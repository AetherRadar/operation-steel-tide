using System;
using Godot;

namespace OperationSteelTide;

internal readonly record struct FieldSupplySnapshot(
    int Bandages,
    int FieldMedkits,
    int Adrenaline,
    int ArmorPlates);

public partial class TacticalPlayer
{
    public bool MedicalActionBlocksWeapon => _medicalActionRemaining > 0.0f;
    public bool AdrenalineActive => _adrenalineRemaining > 0.0f;
    public float AdrenalineRemaining => _adrenalineRemaining;
    public float MedicalMovementMultiplier => AdrenalineActive ? 1.12f : MedicalActionBlocksWeapon ? 0.58f : 1.0f;
    public float MedicalStaminaDrainMultiplier => AdrenalineActive ? 0.34f : 1.0f;
    public float MedicalStaminaRecoveryMultiplier => AdrenalineActive ? 2.25f : 1.0f;
    public float MedicalUseProgress => MedicalActionBlocksWeapon
        ? 1.0f - _medicalActionRemaining / Mathf.Max(0.01f, _medicalActionDuration)
        : 0.0f;

    private FirstPersonFieldUsePresentation? _fieldUsePresentation;
    private float _medicalActionRemaining;
    private float _medicalActionDuration;
    private MedicalItemKind _medicalActionKind;
    private float _adrenalineRemaining;

    public int MedicalCount(MedicalItemKind kind)
    {
        var supplies = CaptureFieldSupplySnapshot();
        return kind switch
        {
            MedicalItemKind.FieldMedkit => supplies.FieldMedkits,
            MedicalItemKind.Adrenaline => supplies.Adrenaline,
            _ => supplies.Bandages
        };
    }

    internal FieldSupplySnapshot CaptureFieldSupplySnapshot()
    {
        var bandages = 0;
        var fieldMedkits = 0;
        var adrenaline = 0;
        var armorPlates = 0;
        foreach (var item in Backpack)
        {
            var quantity = Mathf.Max(0, item.Quantity);
            if (item.Kind == LootItemKind.ArmorPlate)
            {
                armorPlates += quantity;
                continue;
            }
            if (item.Kind != LootItemKind.Medical)
            {
                continue;
            }
            switch (item.MedicalKind)
            {
                case MedicalItemKind.FieldMedkit:
                    fieldMedkits += quantity;
                    break;
                case MedicalItemKind.Adrenaline:
                    adrenaline += quantity;
                    break;
                default:
                    bandages += quantity;
                    break;
            }
        }
        return new FieldSupplySnapshot(bandages, fieldMedkits, adrenaline, armorPlates);
    }

    public int FieldUseCount(FieldUseKind kind)
    {
        return kind == FieldUseKind.ArmorPlate
            ? ArmorPlates
            : MedicalCount(FieldUseItems.ToMedical(kind));
    }

    private void EnsureEmergencyMedicalLoadout()
    {
        if (MedicalCount(MedicalItemKind.Bandage) > 0)
        {
            return;
        }
        Backpack.Add(new LootItem
        {
            Kind = LootItemKind.Medical,
            MedicalKind = MedicalItemKind.Bandage,
            Quantity = 1,
            Grade = LootGrade.Common
        });
        Hud?.SetBackpackValuePlayer(this);
        Hud?.SetMedicalInventory(this);
    }

    private void EnsureEmergencyArmorLoadout()
    {
        if (ArmorPlates > 0)
        {
            return;
        }
        TryStoreArmorPlate(LootGrade.Common, 2);
    }

    public bool TryStartFieldUse(FieldUseKind kind, string preferredItemId = "")
    {
        return kind == FieldUseKind.ArmorPlate
            ? StartPlate(preferredItemId)
            : TryStartMedicalUse(FieldUseItems.ToMedical(kind));
    }

    public bool TryStartMedicalUse(MedicalItemKind kind)
    {
        if (IsDead
            || UiLocked
            || IsInVehicle
            || MedicalActionBlocksWeapon
            || RoleActionBlocksWeapon
            || _isReloading
            || _isPlating
            || MedicalCount(kind) <= 0)
        {
            if (MedicalCount(kind) <= 0)
            {
                Hud?.ShowLocalizedMessage("medical_empty", "NO MEDICAL SUPPLIES", new Color(1.0f, 0.42f, 0.24f));
            }
            return false;
        }
        if (kind is MedicalItemKind.Bandage or MedicalItemKind.FieldMedkit && Health >= MaxHealth - 0.01f)
        {
            Hud?.ShowLocalizedMessage("medical_full", "VITALS ALREADY STABLE", new Color(0.54f, 0.76f, 0.7f));
            return false;
        }

        var definition = MedicalItems.Definition(kind);
        _medicalActionKind = kind;
        _medicalActionDuration = definition.UseDuration;
        _medicalActionRemaining = definition.UseDuration;
        _isAiming = false;
        _slideTime = 0.0f;
        SetMedicalDeviceVisibility();
        Hud?.SetEquipmentActionLocalized(
            kind switch
            {
                MedicalItemKind.FieldMedkit => "medical_using_medkit",
                MedicalItemKind.Adrenaline => "medical_using_adrenaline",
                _ => "medical_using_bandage"
            },
            kind switch
            {
                MedicalItemKind.FieldMedkit => "APPLYING TRAUMA KIT",
                MedicalItemKind.Adrenaline => "INJECTING ADRENALINE",
                _ => "APPLYING BANDAGE"
            },
            0.0f,
            true);
        return true;
    }

    private void UpdateMedicalSystem(float delta)
    {
        _adrenalineRemaining = Mathf.Max(0.0f, _adrenalineRemaining - delta);
        if (!MedicalActionBlocksWeapon)
        {
            SetMedicalDeviceVisibility();
            return;
        }

        _medicalActionRemaining = Mathf.Max(0.0f, _medicalActionRemaining - delta);
        AnimateMedicalDevice();
        var key = _medicalActionKind switch
        {
            MedicalItemKind.FieldMedkit => "medical_using_medkit",
            MedicalItemKind.Adrenaline => "medical_using_adrenaline",
            _ => "medical_using_bandage"
        };
        var english = _medicalActionKind switch
        {
            MedicalItemKind.FieldMedkit => "APPLYING TRAUMA KIT",
            MedicalItemKind.Adrenaline => "INJECTING ADRENALINE",
            _ => "APPLYING BANDAGE"
        };
        Hud?.SetEquipmentActionLocalized(key, english, MedicalUseProgress, true);
        if (_medicalActionRemaining <= 0.0f)
        {
            CompleteMedicalUse();
        }
    }

    private void CompleteMedicalUse()
    {
        var kind = _medicalActionKind;
        var index = Backpack.FindIndex(item => item.Kind == LootItemKind.Medical
            && item.MedicalKind == kind
            && item.Quantity > 0);
        if (index < 0)
        {
            CancelMedicalUse(false);
            return;
        }
        var stack = Backpack[index];
        stack.Quantity--;
        if (stack.Quantity <= 0)
        {
            Backpack.RemoveAt(index);
        }

        var definition = MedicalItems.Definition(kind);
        RestoreHealth(definition.HealthRestore);
        if (kind == MedicalItemKind.Adrenaline)
        {
            Stamina = 100.0f;
            _adrenalineRemaining = 14.0f;
            _skillCooldownRemaining = Mathf.Max(0.0f, _skillCooldownRemaining - 6.0f);
        }
        _medicalActionRemaining = 0.0f;
        _medicalActionDuration = 0.0f;
        SetMedicalDeviceVisibility();
        Hud?.SetEquipmentAction(string.Empty, 0.0f, false);
        Hud?.SetBackpackValuePlayer(this);
        Hud?.SetMedicalInventory(this);
        Hud?.ShowLocalizedMessage(
            kind switch
            {
                MedicalItemKind.FieldMedkit => "medical_medkit_used",
                MedicalItemKind.Adrenaline => "medical_adrenaline_used",
                _ => "medical_bandage_used"
            },
            kind switch
            {
                MedicalItemKind.FieldMedkit => "TRAUMA KIT APPLIED  //  VITALS RESTORED",
                MedicalItemKind.Adrenaline => "ADRENALINE ACTIVE  //  SPEED + STAMINA",
                _ => "BLEEDING CONTROLLED  //  VITALS RESTORED"
            },
            definition.Accent);
    }

    private void CancelMedicalUse(bool notify = true)
    {
        // CompleteMedicalUse can discover that the selected stack was removed
        // after the timer reached zero. Duration remains the lifecycle token in
        // that path, so cleanup must not depend on Remaining alone.
        if (!MedicalActionBlocksWeapon && _medicalActionDuration <= 0.0f)
        {
            return;
        }
        _medicalActionRemaining = 0.0f;
        _medicalActionDuration = 0.0f;
        SetMedicalDeviceVisibility();
        Hud?.SetEquipmentAction(string.Empty, 0.0f, false);
        if (notify)
        {
            Hud?.ShowLocalizedMessage("medical_interrupted", "MEDICAL USE INTERRUPTED", new Color(1.0f, 0.42f, 0.24f));
        }
    }

    private bool HandleMedicalWheelInput()
    {
        if (Hud is null)
        {
            return false;
        }
        if (Hud.IsMedicalWheelVisible)
        {
            Velocity = new Vector3(0.0f, Velocity.Y, 0.0f);
            _isAiming = false;
            if (Hud.TryTakeMedicalWheelConfirmation(out var confirmed))
            {
                FinishMedicalWheel();
                TryStartFieldUse(confirmed);
                return true;
            }
            if (!Input.IsActionPressed(GameInputActions.MedicalWheel))
            {
                var accepted = Hud.CloseMedicalWheel(true, out var highlighted);
                FinishMedicalWheel();
                if (accepted)
                {
                    TryStartFieldUse(highlighted);
                }
                else
                {
                    Hud.ShowLocalizedMessage("field_supply_empty", "NO FIELD SUPPLIES", new Color(1.0f, 0.42f, 0.24f));
                }
                return true;
            }
            return true;
        }
        if (!Input.IsActionJustPressed(GameInputActions.MedicalWheel)
            || IsDead
            || IsInVehicle
            || UiLocked
            || MedicalActionBlocksWeapon
            || RoleActionBlocksWeapon
            || _isPlating
            || _isReloading)
        {
            return false;
        }
        if (!Hud.OpenMedicalWheel(this))
        {
            return false;
        }
        UiLocked = true;
        _isAiming = false;
        DisarmFireInput();
        DisarmMovementInput();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        return true;
    }

    private void FinishMedicalWheel()
    {
        UiLocked = false;
        DisarmFireInput();
        RestoreMovementInput();
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void CloseMedicalWheelWithoutUse()
    {
        if (Hud?.IsMedicalWheelVisible != true)
        {
            return;
        }
        Hud.CancelMedicalWheel();
        FinishMedicalWheel();
    }

    private void BuildMedicalDevices()
    {
        try
        {
            _fieldUsePresentation = new FirstPersonFieldUsePresentation(_camera);
        }
        catch (Exception exception)
        {
            _fieldUsePresentation = null;
            GD.PushWarning($"Authored field-use presentation unavailable: {exception.Message}");
        }
    }

    private void CancelFieldUse(bool notify = false)
    {
        CancelMedicalUse(notify);
        CancelPlate(notify);
        SetMedicalDeviceVisibility();
    }

    private void ResetFieldUseForRound()
    {
        CancelFieldUse(false);
        _adrenalineRemaining = 0.0f;
    }

    private void SetMedicalDeviceVisibility()
    {
        if (_fieldUsePresentation is null)
        {
            return;
        }
        if (MedicalActionBlocksWeapon)
        {
            _weaponRoot.Visible = false;
            _knifeRoot.Visible = false;
            UpdateHeldThrowableVisual();
            _fieldUsePresentation.Present(
                _medicalActionKind switch
                {
                    MedicalItemKind.FieldMedkit => FirstPersonFieldUsePresentationKind.FieldMedkit,
                    MedicalItemKind.Adrenaline => FirstPersonFieldUsePresentationKind.Adrenaline,
                    _ => FirstPersonFieldUsePresentationKind.Bandage
                },
                MedicalUseProgress);
            return;
        }
        if (_isPlating)
        {
            _weaponRoot.Visible = false;
            _knifeRoot.Visible = false;
            UpdateHeldThrowableVisual();
            _fieldUsePresentation.Present(
                FirstPersonFieldUsePresentationKind.ArmorPlate,
                1.0f - _plateTime / Mathf.Max(0.01f, _plateDuration));
            return;
        }
        _fieldUsePresentation.Hide();
        UpdateHeldItemVisibility();
    }

    private void AnimateMedicalDevice()
    {
        SetMedicalDeviceVisibility();
    }

    internal void GrantMedicalItemForDiagnostics(MedicalItemKind kind, int quantity)
    {
        if (quantity <= 0)
        {
            return;
        }
        var stack = Backpack.Find(item => item.Kind == LootItemKind.Medical && item.MedicalKind == kind);
        if (stack is not null)
        {
            stack.Quantity += quantity;
        }
        else
        {
            Backpack.Add(new LootItem
            {
                Kind = LootItemKind.Medical,
                MedicalKind = kind,
                Quantity = quantity,
                Grade = kind == MedicalItemKind.Adrenaline ? LootGrade.Rare : LootGrade.Uncommon
            });
        }
        Hud?.SetMedicalInventory(this);
    }

    internal bool CompleteMedicalUseForDiagnostics()
    {
        if (!MedicalActionBlocksWeapon)
        {
            return false;
        }
        _medicalActionRemaining = 0.0f;
        CompleteMedicalUse();
        return true;
    }

    internal void SetStaminaForDiagnostics(float value)
    {
        Stamina = Mathf.Clamp(value, 0.0f, 100.0f);
        _sprintRecoveryRequired = Stamina <= 0.01f;
        _sprintRecoveryDelay = _sprintRecoveryRequired ? SprintRecoveryDelay : 0.0f;
    }

    internal void AdvanceStaminaForDiagnostics(float delta, bool sprintRequested)
    {
        var sprinting = sprintRequested && Stamina > 1.0f && !SprintRecoveryRequired;
        UpdateStaminaState(delta, sprinting);
    }

    internal float SprintRecoveryThresholdForDiagnostics => SprintRecoveryThreshold;

    internal bool IsPlateUseActiveForDiagnostics => _isPlating;

    internal float PlateUseRemainingForDiagnostics => _plateTime;

    internal void SetArmorForDiagnostics(float percent)
    {
        EquippedBodyArmor.Durability = EquippedBodyArmor.Definition.MaxDurability
            * Mathf.Clamp(percent, 0.0f, 100.0f) / 100.0f;
    }

    internal bool CompletePlateUseForDiagnostics()
    {
        if (!_isPlating)
        {
            return false;
        }
        _plateTime = 0.0f;
        UpdatePlate(0.0f);
        return !_isPlating;
    }
}
