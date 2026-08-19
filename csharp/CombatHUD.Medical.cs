using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private Control _medicalStatusRoot = null!;
    private Label _medicalStatusLabel = null!;
    private Label _medicalBoostLabel = null!;
    private ColorRect _medicalWheelOverlay = null!;
    private MedicalWheelControl _medicalWheel = null!;
    private Label _medicalWheelTitle = null!;
    private Label _medicalWheelHint = null!;
    private Label _medicalWheelVitals = null!;
    private FieldUseKind? _medicalConfirmed;

    public bool IsMedicalWheelVisible => IsInstanceValid(_medicalWheelOverlay) && _medicalWheelOverlay.Visible;

    private void BuildMedicalHud(Control root)
    {
        _medicalStatusRoot = Panel(root, Vector2.Zero, new Vector2(245, 46));
        _medicalStatusRoot.AnchorLeft = 0.0f;
        _medicalStatusRoot.AnchorTop = 1.0f;
        _medicalStatusRoot.AnchorRight = 0.0f;
        _medicalStatusRoot.AnchorBottom = 1.0f;
        _medicalStatusRoot.OffsetLeft = 30;
        _medicalStatusRoot.OffsetTop = -180;
        _medicalStatusRoot.OffsetRight = 275;
        _medicalStatusRoot.OffsetBottom = -134;
        _medicalStatusLabel = Label("B  MEDICAL  //  0  0  0", 12, new Color(0.62f, 0.9f, 0.78f));
        _medicalStatusLabel.Position = new Vector2(18, 6);
        _medicalStatusLabel.Size = new Vector2(214, 20);
        _medicalStatusLabel.ClipText = true;
        _medicalStatusRoot.AddChild(_medicalStatusLabel);
        _medicalBoostLabel = Label(string.Empty, 10, new Color(1.0f, 0.66f, 0.2f));
        _medicalBoostLabel.Position = new Vector2(18, 25);
        _medicalBoostLabel.Size = new Vector2(214, 16);
        _medicalBoostLabel.ClipText = true;
        _medicalStatusRoot.AddChild(_medicalBoostLabel);

        _medicalWheelOverlay = new ColorRect
        {
            Color = new Color(0.004f, 0.008f, 0.009f, 0.78f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        _medicalWheelOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_medicalWheelOverlay);

        _medicalWheel = new MedicalWheelControl
        {
            Size = new Vector2(560, 560),
            Position = new Vector2(-280, -280)
        };
        _medicalWheel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _medicalWheel.Confirmed += kind => _medicalConfirmed = kind;
        _medicalWheelOverlay.AddChild(_medicalWheel);

        _medicalWheelTitle = Label("FIELD SUPPLY WHEEL", 22, new Color(0.72f, 0.96f, 0.87f));
        _medicalWheelTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _medicalWheelTitle.SetAnchorsPreset(Control.LayoutPreset.Center);
        _medicalWheelTitle.Position = new Vector2(-280, -350);
        _medicalWheelTitle.Size = new Vector2(560, 34);
        _medicalWheelOverlay.AddChild(_medicalWheelTitle);
        _medicalWheelHint = Label("POINT + CLICK  //  RELEASE B TO USE", 12, new Color(0.5f, 0.66f, 0.62f));
        _medicalWheelHint.HorizontalAlignment = HorizontalAlignment.Center;
        _medicalWheelHint.SetAnchorsPreset(Control.LayoutPreset.Center);
        _medicalWheelHint.Position = new Vector2(-280, 312);
        _medicalWheelHint.Size = new Vector2(560, 24);
        _medicalWheelOverlay.AddChild(_medicalWheelHint);
        _medicalWheelVitals = Label("HP 100  //  ARM 100  //  STM 100", 13, new Color(0.84f, 0.93f, 0.9f));
        _medicalWheelVitals.HorizontalAlignment = HorizontalAlignment.Center;
        _medicalWheelVitals.VerticalAlignment = VerticalAlignment.Center;
        _medicalWheelVitals.SetAnchorsPreset(Control.LayoutPreset.Center);
        _medicalWheelVitals.Position = new Vector2(-90, -38);
        _medicalWheelVitals.Size = new Vector2(180, 76);
        _medicalWheelVitals.MouseFilter = Control.MouseFilterEnum.Ignore;
        _medicalWheelOverlay.AddChild(_medicalWheelVitals);
    }

    public bool OpenMedicalWheel(TacticalPlayer player)
    {
        if (!IsInstanceValid(_medicalWheelOverlay) || IsLootVisible || IsSquadLobbyVisible)
        {
            return false;
        }
        _medicalConfirmed = null;
        _medicalWheel.Configure(_language, player);
        _medicalWheelVitals.Text = $"HP  {Mathf.CeilToInt(player.Health):000}\nARM {Mathf.CeilToInt(player.Armor):000}\nSTM {Mathf.CeilToInt(player.Stamina):000}";
        RefreshMedicalLanguage();
        _medicalWheelOverlay.Visible = true;
        return true;
    }

    public bool TryTakeMedicalWheelConfirmation(out FieldUseKind kind)
    {
        if (_medicalConfirmed is null)
        {
            kind = default;
            return false;
        }
        kind = _medicalConfirmed.Value;
        _medicalConfirmed = null;
        _medicalWheelOverlay.Visible = false;
        return true;
    }

    public bool CloseMedicalWheel(bool acceptHighlighted, out FieldUseKind kind)
    {
        kind = _medicalWheel.HighlightedKind;
        var accepted = acceptHighlighted && _medicalWheel.HighlightedAvailable;
        _medicalConfirmed = null;
        _medicalWheelOverlay.Visible = false;
        return accepted;
    }

    public void CancelMedicalWheel()
    {
        _medicalConfirmed = null;
        if (IsInstanceValid(_medicalWheelOverlay))
        {
            _medicalWheelOverlay.Visible = false;
        }
    }

    public void SetMedicalInventory(TacticalPlayer player)
    {
        SetMedicalInventory(
            player.CaptureFieldSupplySnapshot(),
            player.AdrenalineActive,
            player.AdrenalineRemaining);
    }

    internal void SetMedicalInventory(
        FieldSupplySnapshot supplies,
        bool adrenalineActive,
        float adrenalineRemaining)
        => ApplyMedicalPresentation(supplies, adrenalineActive, adrenalineRemaining);

    private void RefreshMedicalLanguage()
    {
        if (!IsInstanceValid(_medicalWheelTitle))
        {
            return;
        }
        _medicalWheelTitle.Text = Text("medical_selector", "FIELD SUPPLY WHEEL");
        _medicalWheelHint.Text = Text("medical_wheel_hint", "POINT + CLICK  //  RELEASE B TO USE");
    }

    internal void SelectMedicalWheelForDiagnostics(FieldUseKind kind) => _medicalWheel.SelectForDiagnostics(kind);

    internal void SelectMedicalWheelForDiagnostics(MedicalItemKind kind) => _medicalWheel.SelectForDiagnostics(FieldUseItems.FromMedical(kind));

    internal bool ConfirmMedicalWheelForDiagnostics() => _medicalWheel.ConfirmForDiagnostics();

    internal string MedicalWheelLayoutForDiagnostics()
    {
        return $"overlay={_medicalWheelOverlay.Size} overlay_tree={_medicalWheelOverlay.IsVisibleInTree()} wheel_pos={_medicalWheel.Position} wheel_size={_medicalWheel.Size} wheel_tree={_medicalWheel.IsVisibleInTree()} title_pos={_medicalWheelTitle.Position} title_size={_medicalWheelTitle.Size} title_tree={_medicalWheelTitle.IsVisibleInTree()}";
    }
}
