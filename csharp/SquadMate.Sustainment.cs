using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    private const float SustainmentLootRange = 24.0f;
    private const float SustainmentLootArrivalRange = 1.9f;
    private const float SustainmentRecentDamageSeconds = 3.2f;
    private const float SustainmentLootTravelTimeout = 12.0f;
    private const float SustainmentLootNoProgressTimeout = 4.0f;
    private const float SustainmentLootRetryCooldown = 6.0f;

    private readonly int[,] _medicalSupplies = new int[
        Enum.GetValues<MedicalItemKind>().Length,
        Enum.GetValues<LootGrade>().Length];
    private readonly int[,] _recoveredMedicalSupplies = new int[
        Enum.GetValues<MedicalItemKind>().Length,
        Enum.GetValues<LootGrade>().Length];
    private readonly int[] _armorPlateSupplies = new int[Enum.GetValues<LootGrade>().Length];
    private readonly int[] _recoveredArmorPlateSupplies = new int[Enum.GetValues<LootGrade>().Length];
    private EquipmentItem _equippedHelmet = EquipmentCatalog.Create("helmet_patrol");
    private EquipmentItem _equippedBodyArmor = EquipmentCatalog.Create("armor_patrol");
    private EquipmentItem _equippedBackpack = EquipmentCatalog.Create("pack_sling");
    private LootGrade _equippedHelmetGrade = LootGrade.Uncommon;
    private LootGrade _equippedBodyArmorGrade = LootGrade.Uncommon;
    private LootGrade _equippedBackpackGrade = LootGrade.Uncommon;
    private LootGrade _carriedWeaponGrade = LootGrade.Uncommon;
    private LootGrade _recoveredAmmoGrade = LootGrade.Common;
    private AmmoCaliber _recoveredAmmoCaliber = AmmoCaliber.Rifle;
    private int _recoveredAmmoQuantity;
    private bool _carriedWeaponRecovered;
    private bool _equippedHelmetRecovered;
    private bool _equippedBodyArmorRecovered;
    private bool _equippedBackpackRecovered;
    private ILootSource? _sustainmentLootSource;
    private ulong _sustainmentLootSourceId;
    private ulong _sustainmentLootRetrySourceId;
    private SquadSustainmentActionKind _sustainmentAction;
    private float _sustainmentActionRemaining;
    private float _sustainmentDecisionTimer;
    private float _sustainmentRecentDamageTimer;
    private float _sustainmentLootTravelRemaining;
    private float _sustainmentLootProgressSampleTimer;
    private float _sustainmentLootNoProgressTimer;
    private float _sustainmentLootBestDistanceSquared;
    private float _sustainmentLootRetryTimer;
    private MedicalItemKind _pendingMedicalKind;
    private LootGrade _pendingArmorPlateGrade;

    public EquipmentItem EquippedHelmet => _equippedHelmet;
    public EquipmentItem EquippedBodyArmor => _equippedBodyArmor;
    public EquipmentItem EquippedBackpack => _equippedBackpack;
    public LootGrade CarriedWeaponGrade => _carriedWeaponGrade;
    public float ArmorRatio => _equippedBodyArmor.Definition.MaxDurability <= 0.01f
        ? 0.0f
        : Mathf.Clamp(
            _equippedBodyArmor.Durability / _equippedBodyArmor.Definition.MaxDurability,
            0.0f,
            1.0f);
    public int SustainmentSupplyCount => CountSustainmentSupplies();
    public int RecoveredSustainmentValue => CurrentRecoveredSustainmentValue();

    internal int SustainmentDecisionCountForDiagnostics { get; private set; }
    internal int SustainmentSourceScanCountForDiagnostics { get; private set; }
    internal int SustainmentCompletedActionsForDiagnostics { get; private set; }
    internal SquadSustainmentActionKind SustainmentActionForDiagnostics => _sustainmentAction;
    internal ILootSource? SustainmentLootSourceForDiagnostics => _sustainmentLootSource;
    internal bool IsHealingForSquadRevivePreparation
        => _sustainmentAction == SquadSustainmentActionKind.Heal;
    internal int BandagesForDiagnostics => MedicalCount(MedicalItemKind.Bandage);
    internal int ArmorPlatesForDiagnostics => _armorPlateSupplies.Sum();
    internal int SustainmentSupplyCapacityForWorld => SustainmentSupplyCapacity;

    private void InitializeSustainmentLoadout()
    {
        _equippedHelmet = EquipmentCatalog.Create("helmet_patrol");
        _equippedBodyArmor = EquipmentCatalog.Create("armor_patrol");
        _equippedBackpack = EquipmentCatalog.Create("pack_sling");
        _equippedHelmetGrade = LootGrade.Uncommon;
        _equippedBodyArmorGrade = LootGrade.Uncommon;
        _equippedBackpackGrade = LootGrade.Uncommon;
        _carriedWeaponGrade = LootGrade.Uncommon;
        ResetRecoveredAmmo();
        _carriedWeaponRecovered = false;
        _equippedHelmetRecovered = false;
        _equippedBodyArmorRecovered = false;
        _equippedBackpackRecovered = false;
        Array.Clear(_medicalSupplies);
        Array.Clear(_recoveredMedicalSupplies);
        Array.Clear(_armorPlateSupplies);
        Array.Clear(_recoveredArmorPlateSupplies);
        _medicalSupplies[
            (int)(Role == OperatorRole.Medic
                ? MedicalItemKind.FieldMedkit
                : MedicalItemKind.Bandage),
            (int)LootGrade.Uncommon] = 1;
        _armorPlateSupplies[(int)LootGrade.Uncommon] = 1;
        _sustainmentAction = SquadSustainmentActionKind.None;
        _sustainmentActionRemaining = 0.0f;
        _sustainmentDecisionTimer = 0.65f + SquadSlot * 0.23f;
        _sustainmentRecentDamageTimer = 0.0f;
        _sustainmentLootSource = null;
        _sustainmentLootSourceId = 0;
        _sustainmentLootRetrySourceId = 0;
        _sustainmentLootRetryTimer = 0.0f;
    }

    private void UpdateSustainmentTimers(float delta)
    {
        _sustainmentDecisionTimer = Mathf.Max(0.0f, _sustainmentDecisionTimer - delta);
        _sustainmentRecentDamageTimer = Mathf.Max(0.0f, _sustainmentRecentDamageTimer - delta);
        _sustainmentLootRetryTimer = Mathf.Max(0.0f, _sustainmentLootRetryTimer - delta);
        if (_sustainmentLootRetryTimer <= 0.0f)
        {
            _sustainmentLootRetrySourceId = 0;
        }
    }

    /// <summary>
    /// Advances an already chosen low-frequency sustainment intent. Returning true
    /// means the operator is channeling an item/search and should hold movement.
    /// </summary>
    private bool UpdateSustainment(float delta, EnemyOperator? hostile)
    {
        if (!Main.IsSquadSustainmentEnabled)
        {
            CancelSustainmentAction(releaseLoot: true);
            return false;
        }
        var hostileDistance = hostile is null || !IsInstanceValid(hostile) || hostile.IsDead
            ? float.PositiveInfinity
            : GlobalPosition.DistanceTo(hostile.GlobalPosition);
        var urgentSquadState = HasActiveReviveTarget
            || Main.ShouldSuppressSquadLooting(this);
        var evacuating = Main.IsSquadEvacuationInProgress;
        var actionSafe = SquadSustainmentRules.CanStartSustainment(
            IsDowned,
            IsBodyBag,
            HasActiveReviveTarget,
            evacuating,
            _sustainmentRecentDamageTimer > 0.0f,
            hostileDistance);

        if (_sustainmentAction != SquadSustainmentActionKind.None)
        {
            if (!actionSafe
                || HasActiveReviveTarget
                || _sustainmentAction == SquadSustainmentActionKind.Loot
                    && (urgentSquadState || Order != SquadOrder.Follow))
            {
                CancelSustainmentAction(releaseLoot: true);
                return false;
            }
            Velocity = Velocity.MoveToward(Vector3.Zero, delta * 20.0f);
            _sustainmentActionRemaining -= delta;
            if (_sustainmentActionRemaining <= 0.0f)
            {
                CompleteSustainmentAction();
            }
            return true;
        }

        if (_sustainmentLootSource is not null
            && (!IsInstanceValid(_sustainmentLootSource.LootNode)
                || !_sustainmentLootSource.IsSearchable
                || urgentSquadState
                || Order != SquadOrder.Follow
                || !Main.IsSquadSustainmentReservationOwner(
                    this,
                    _sustainmentLootSourceId)
                || hostileDistance < SquadSustainmentRules.NearbyThreatRange))
        {
            ClearSustainmentLootTarget();
        }
        else if (_sustainmentLootSource is not null
            && !AdvanceSustainmentLootTravel(delta))
        {
            AbandonSustainmentLootTarget();
        }

        if (_sustainmentDecisionTimer > 0.0f)
        {
            return false;
        }
        _sustainmentDecisionTimer = 1.55f + SquadSlot * 0.17f;
        SustainmentDecisionCountForDiagnostics++;

        if (actionSafe && TryBeginSelfCare())
        {
            return true;
        }
        if (!actionSafe || urgentSquadState || Order != SquadOrder.Follow)
        {
            return false;
        }

        // Cold-start re-arming owns sealed weapon caches. General sustainment
        // begins only after the operator has a usable primary.
        if (!HasFireablePrimary)
        {
            return false;
        }

        if (_sustainmentLootSource is null)
        {
            SustainmentSourceScanCountForDiagnostics++;
            if (Main.TryReserveBestSquadSustainmentSource(
                this,
                SustainmentLootRange,
                out var reservedSource))
            {
                SetSustainmentLootTarget(reservedSource);
            }
        }
        if (_sustainmentLootSource is null || !IsInstanceValid(_sustainmentLootSource.LootNode))
        {
            return false;
        }
        if (GlobalPosition.DistanceTo(_sustainmentLootSource.LootNode.GlobalPosition)
            <= SustainmentLootArrivalRange)
        {
            _sustainmentAction = SquadSustainmentActionKind.Loot;
            _sustainmentActionRemaining = Mathf.Clamp(
                _sustainmentLootSource.SearchDuration,
                0.55f,
                1.15f);
            return true;
        }
        return false;
    }

    private bool TryGetSustainmentDestination(out Vector3 destination)
    {
        if (_sustainmentLootSource is not null
            && IsInstanceValid(_sustainmentLootSource.LootNode)
            && _sustainmentLootSource.IsSearchable)
        {
            destination = _sustainmentLootSource.LootNode.GlobalPosition;
            return true;
        }
        destination = Vector3.Zero;
        return false;
    }

    private bool TryBeginSelfCare()
    {
        var medical = SquadSustainmentRules.SelectMedical(
            Health,
            MaxHealth,
            MedicalCount(MedicalItemKind.Bandage),
            MedicalCount(MedicalItemKind.FieldMedkit),
            MedicalCount(MedicalItemKind.Adrenaline));
        if (medical.HasValue)
        {
            _pendingMedicalKind = medical.Value;
            _sustainmentAction = SquadSustainmentActionKind.Heal;
            _sustainmentActionRemaining = MedicalItems.Definition(medical.Value).UseDuration;
            ClearSustainmentLootTarget();
            return true;
        }

        if (ArmorRatio >= SquadSustainmentRules.ArmorRepairRatio
            || !TrySelectArmorPlate(out var plateGrade))
        {
            return false;
        }
        _pendingArmorPlateGrade = plateGrade;
        _sustainmentAction = SquadSustainmentActionKind.RepairArmor;
        _sustainmentActionRemaining = Mathf.Max(1.65f, 2.55f - (int)plateGrade * 0.16f);
        ClearSustainmentLootTarget();
        return true;
    }

    internal bool CanSelfCareBeforeSquadRevive(float minimumHealthRatio)
    {
        if (IsDowned
            || IsBodyBag
            || IsHumanProxy
            || IsNetworkProxy
            || IsExtractionPassenger
            || Health / Mathf.Max(1.0f, MaxHealth) >= minimumHealthRatio)
        {
            return false;
        }
        return SquadSustainmentRules.SelectMedical(
            Health,
            MaxHealth,
            MedicalCount(MedicalItemKind.Bandage),
            MedicalCount(MedicalItemKind.FieldMedkit),
            MedicalCount(MedicalItemKind.Adrenaline)).HasValue;
    }

    private void CompleteSustainmentAction()
    {
        switch (_sustainmentAction)
        {
            case SquadSustainmentActionKind.Heal:
                if (ConsumeMedical(_pendingMedicalKind))
                {
                    RestoreHealth(MedicalItems.Definition(_pendingMedicalKind).HealthRestore);
                    SustainmentCompletedActionsForDiagnostics++;
                }
                break;
            case SquadSustainmentActionKind.RepairArmor:
                if (ConsumeArmorPlate(_pendingArmorPlateGrade))
                {
                    var definition = _equippedBodyArmor.Definition;
                    _equippedBodyArmor.Durability = Mathf.Min(
                        definition.MaxDurability,
                        _equippedBodyArmor.Durability
                            + definition.MaxDurability
                                * ArmorPlateSupplies.RepairFraction(_pendingArmorPlateGrade));
                    SustainmentCompletedActionsForDiagnostics++;
                }
                break;
            case SquadSustainmentActionKind.Loot:
                if (_sustainmentLootSource is not null
                    && Main.TryMateTakeSustainmentLoot(this, _sustainmentLootSource))
                {
                    SustainmentCompletedActionsForDiagnostics++;
                }
                ClearSustainmentLootTarget();
                break;
        }
        _sustainmentAction = SquadSustainmentActionKind.None;
        _sustainmentActionRemaining = 0.0f;
        _sustainmentDecisionTimer = 0.8f + SquadSlot * 0.12f;
    }

    private void RegisterSustainmentDamage()
    {
        _sustainmentRecentDamageTimer = SustainmentRecentDamageSeconds;
        CancelSustainmentAction(releaseLoot: true);
    }

    private void CancelSustainmentAction(bool releaseLoot)
    {
        _sustainmentAction = SquadSustainmentActionKind.None;
        _sustainmentActionRemaining = 0.0f;
        if (releaseLoot)
        {
            ClearSustainmentLootTarget();
        }
    }

    private void ClearSustainmentLootTarget()
    {
        if (_sustainmentLootSourceId != 0 && IsInstanceValid(Main))
        {
            Main.ReleaseSquadSustainmentSource(this, _sustainmentLootSourceId);
        }
        _sustainmentLootSource = null;
        _sustainmentLootSourceId = 0;
        _sustainmentLootTravelRemaining = 0.0f;
        _sustainmentLootProgressSampleTimer = 0.0f;
        _sustainmentLootNoProgressTimer = 0.0f;
        _sustainmentLootBestDistanceSquared = float.PositiveInfinity;
    }

    private void SetSustainmentLootTarget(ILootSource? source)
    {
        if (source is null || !IsInstanceValid(source.LootNode))
        {
            return;
        }
        _sustainmentLootSource = source;
        _sustainmentLootSourceId = source.LootNode.GetInstanceId();
        _sustainmentLootTravelRemaining = SustainmentLootTravelTimeout;
        _sustainmentLootProgressSampleTimer = 0.75f;
        _sustainmentLootNoProgressTimer = 0.0f;
        _sustainmentLootBestDistanceSquared = GlobalPosition.DistanceSquaredTo(
            source.LootNode.GlobalPosition);
    }

    private bool AdvanceSustainmentLootTravel(float delta)
    {
        if (_sustainmentLootSource is null || !IsInstanceValid(_sustainmentLootSource.LootNode))
        {
            return false;
        }
        var distanceSquared = GlobalPosition.DistanceSquaredTo(
            _sustainmentLootSource.LootNode.GlobalPosition);
        if (distanceSquared <= SustainmentLootArrivalRange * SustainmentLootArrivalRange)
        {
            return true;
        }
        _sustainmentLootTravelRemaining -= Mathf.Max(0.0f, delta);
        _sustainmentLootProgressSampleTimer -= Mathf.Max(0.0f, delta);
        if (_sustainmentLootProgressSampleTimer <= 0.0f)
        {
            if (distanceSquared + 1.0f < _sustainmentLootBestDistanceSquared)
            {
                _sustainmentLootBestDistanceSquared = distanceSquared;
                _sustainmentLootNoProgressTimer = 0.0f;
            }
            else
            {
                _sustainmentLootNoProgressTimer += 0.75f;
            }
            _sustainmentLootProgressSampleTimer = 0.75f;
        }
        return _sustainmentLootTravelRemaining > 0.0f
            && _sustainmentLootNoProgressTimer < SustainmentLootNoProgressTimeout;
    }

    private void AbandonSustainmentLootTarget()
    {
        _sustainmentLootRetrySourceId = _sustainmentLootSourceId;
        _sustainmentLootRetryTimer = SustainmentLootRetryCooldown;
        ClearSustainmentLootTarget();
    }

    internal bool IsSustainmentSourceCoolingDown(ILootSource source)
        => _sustainmentLootRetryTimer > 0.0f
            && IsInstanceValid(source.LootNode)
            && source.LootNode.GetInstanceId() == _sustainmentLootRetrySourceId;

    private void ResetSustainmentForIncapacitation()
    {
        CancelSustainmentAction(releaseLoot: true);
        _sustainmentDecisionTimer = 1.0f;
    }

    private float ApplySustainmentProtection(HitRegion region, float damage)
    {
        if (IsHumanProxy || IsNetworkProxy || !Main.IsSquadSustainmentEnabled)
        {
            return damage;
        }
        var equipment = region switch
        {
            HitRegion.Head => _equippedHelmet,
            HitRegion.Torso => _equippedBodyArmor,
            _ => null
        };
        if (equipment is null
            || equipment.Durability <= 0.0f
            || equipment.Definition.Protection <= 0.0f)
        {
            return damage;
        }
        var definition = equipment.Definition;
        var durabilityRatio = Mathf.Clamp(
            equipment.Durability / Mathf.Max(1.0f, definition.MaxDurability),
            0.0f,
            1.0f);
        var effectiveProtection = definition.Protection * Mathf.Lerp(0.55f, 1.0f, durabilityRatio);
        equipment.Durability = Mathf.Max(0.0f, equipment.Durability - damage * 0.58f);
        return damage * (1.0f - effectiveProtection);
    }

}
