using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    public bool IsOpened => IsInstanceValid(_corpseLootBackpack) && _corpseLootBackpack.IsOpened;
    public bool CorpseBackpackVisualReady => IsInstanceValid(_corpseLootBackpack)
        && _corpseLootBackpack.Visible
        && _corpseLootBackpack.VisualReady;
    public bool CorpseBackpackOpenVisualReady => IsInstanceValid(_corpseLootBackpack)
        && _corpseLootBackpack.Visible
        && _corpseLootBackpack.OpenVisualReady;
    public float CorpseBackpackFlapRotationForDiagnostics => IsInstanceValid(_corpseLootBackpack)
        ? _corpseLootBackpack.FlapRotationForDiagnostics
        : float.NaN;
    public int CorpseBackpackOpenRequestsForDiagnostics { get; private set; }
    public int CorpseBackpackOpenBlockedDeadForDiagnostics { get; private set; }
    public int CorpseBackpackOpenBlockedVisualForDiagnostics { get; private set; }

    private LootBackpackVisual _corpseLootBackpack = null!;

    private void BuildCorpseLootBackpack()
    {
        _corpseLootBackpack = new LootBackpackVisual
        {
            Name = "CorpseLootBackpack",
            Position = new Vector3(0.62f, 0.04f, 0.22f),
            Rotation = new Vector3(0.0f, -0.28f, 0.0f),
            Scale = Vector3.One * 0.92f,
            Visible = false
        };
        AddChild(_corpseLootBackpack);
    }

    private void ShowCorpseLootBackpack()
    {
        if (!IsInstanceValid(_corpseLootBackpack))
        {
            BuildCorpseLootBackpack();
        }

        _corpseLootBackpack.Visible = true;
    }

    private void OpenCorpseLootBackpack()
    {
        CorpseBackpackOpenRequestsForDiagnostics++;
        if (!IsDead)
        {
            CorpseBackpackOpenBlockedDeadForDiagnostics++;
            return;
        }
        if (!IsInstanceValid(_corpseLootBackpack))
        {
            CorpseBackpackOpenBlockedVisualForDiagnostics++;
            return;
        }

        _corpseLootBackpack.Open();
    }

    private void ResetCorpseLootBackpackForDiagnostics()
    {
        if (IsInstanceValid(_corpseLootBackpack))
        {
            _corpseLootBackpack.Visible = false;
            _corpseLootBackpack.QueueFree();
        }

        _corpseLootBackpack = null!;
        CorpseBackpackOpenRequestsForDiagnostics = 0;
        CorpseBackpackOpenBlockedDeadForDiagnostics = 0;
        CorpseBackpackOpenBlockedVisualForDiagnostics = 0;
    }
}
