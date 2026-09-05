using Godot;

namespace OperationSteelTide;

/// <summary>Reusable visual card that reports a chosen attachment without mutating the build.</summary>
[GlobalClass]
public partial class TrainingRangeArmoryPartCard : Button
{
    [Signal] public delegate void ChosenEventHandler(string attachmentId);

    private Label _name = null!;
    private Label _effect = null!;
    private Label _badge = null!;
    private ColorRect _accent = null!;
    private bool _bound;

    public string AttachmentId { get; private set; } = string.Empty;

    public override void _Ready()
    {
        _name = GetNode<Label>("Content/Name");
        _effect = GetNode<Label>("Content/Effect");
        _badge = GetNode<Label>("Content/Badge");
        _accent = GetNode<ColorRect>("Accent");
        Pressed += HandlePressed;
        _bound = true;
    }

    public void Configure(
        string attachmentId,
        string displayName,
        string effect,
        bool installed,
        bool locked,
        string language)
    {
        AttachmentId = attachmentId ?? string.Empty;
        if (!_bound)
        {
            return;
        }
        _name.Text = displayName;
        _effect.Text = effect;
        _badge.Text = installed
            ? language == "zh" ? "已装备" : "INSTALLED"
            : string.Empty;
        _badge.Visible = installed;
        Disabled = locked;
        ButtonPressed = installed;
        _accent.Color = locked
            ? new Color(0.28f, 0.34f, 0.34f)
            : installed
                ? new Color(0.3f, 0.95f, 0.72f)
                : new Color(0.14f, 0.38f, 0.37f);
    }

    private void HandlePressed()
    {
        if (!Disabled)
        {
            EmitSignal(SignalName.Chosen, AttachmentId);
        }
    }
}
