using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    public bool IsScanned { get; private set; }

    private Node3D? _scanMarker;
    private int _scanGeneration;

    public async void SetScanned(float duration)
    {
        if (IsDead)
        {
            return;
        }
        IsScanned = true;
        var generation = ++_scanGeneration;
        if (!IsInstanceValid(_scanMarker))
        {
            _scanMarker = BuildScanMarker();
            AddChild(_scanMarker);
        }
        _scanMarker.Visible = true;
        await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);
        if (generation != _scanGeneration || !IsInstanceValid(this))
        {
            return;
        }
        IsScanned = false;
        if (IsInstanceValid(_scanMarker))
        {
            _scanMarker!.Visible = false;
        }
    }

    private static Node3D BuildScanMarker()
    {
        var root = new Node3D { Name = "ReconScanMarker" };
        var color = new Color(0.2f, 0.72f, 1.0f, 0.8f);
        var material = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = color,
            EmissionEnabled = true,
            Emission = new Color(color.R, color.G, color.B),
            EmissionEnergyMultiplier = 2.8f,
            NoDepthTest = true
        };
        root.AddChild(new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.32f, OuterRadius = 0.38f, Rings = 28, RingSegments = 8 },
            Position = new Vector3(0.0f, 1.15f, 0.0f),
            Rotation = new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f),
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        var label = new Label3D
        {
            Text = "HOSTILE  //  SCANNED",
            Position = new Vector3(0.0f, 2.18f, 0.0f),
            FontSize = 18,
            OutlineSize = 7,
            Modulate = color,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true
        };
        root.AddChild(label);
        return root;
    }
}
