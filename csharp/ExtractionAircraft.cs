using Godot;

namespace OperationSteelTide;

public enum ExtractionAircraftPhase
{
    Hidden,
    Inbound,
    Boarding,
    Departing
}

[GlobalClass]
public partial class ExtractionAircraft : Node3D
{
    public const float ArrivalDuration = 6.4f;

    public Vector3 PadPosition { get; set; }
    public ExtractionAircraftPhase Phase { get; private set; } = ExtractionAircraftPhase.Hidden;
    public bool BoardingReady => Phase == ExtractionAircraftPhase.Boarding;
    public float ArrivalProgress => Phase == ExtractionAircraftPhase.Inbound
        ? Mathf.Clamp(_phaseElapsed / ArrivalDuration, 0.0f, 1.0f)
        : Phase == ExtractionAircraftPhase.Hidden ? 0.0f : 1.0f;

    private Node3D _visual = null!;
    private Node3D _leftRotor = null!;
    private Node3D _rightRotor = null!;
    private Node3D _ramp = null!;
    private Node3D _dust = null!;
    private AudioStreamPlayer3D _rotorAudio = null!;
    private Vector3 _startPosition;
    private Vector3 _departureStart;
    private Vector3 _departureTarget;
    private float _phaseElapsed;

    public override void _Ready()
    {
        BuildVisuals();
        Visible = false;
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        AdvanceFlight((float)delta);
    }

    public void BeginInbound()
    {
        _startPosition = PadPosition + new Vector3(104.0f, 36.0f, 78.0f);
        Position = _startPosition;
        Rotation = new Vector3(0.08f, -2.22f, -0.1f);
        _phaseElapsed = 0.0f;
        Phase = ExtractionAircraftPhase.Inbound;
        Visible = true;
        _ramp.Rotation = Vector3.Zero;
        _dust.Visible = false;
        SetProcess(true);
        if (!_rotorAudio.Playing)
        {
            _rotorAudio.Play();
        }
    }

    public void AbortPickup()
    {
        if (Phase == ExtractionAircraftPhase.Hidden)
        {
            return;
        }
        BeginDeparture(PadPosition + new Vector3(72.0f, 32.0f, 66.0f));
    }

    public void BeginDeparture()
    {
        BeginDeparture(PadPosition + new Vector3(-96.0f, 42.0f, -78.0f));
    }

    public void ForceBoardingReadyForValidation()
    {
        Visible = true;
        Position = BoardingPosition();
        Rotation = Vector3.Zero;
        _phaseElapsed = ArrivalDuration;
        Phase = ExtractionAircraftPhase.Boarding;
        _ramp.Rotation = new Vector3(-0.58f, 0.0f, 0.0f);
        _dust.Visible = true;
        SetProcess(true);
        if (!_rotorAudio.Playing)
        {
            _rotorAudio.Play();
        }
    }

    private void BeginDeparture(Vector3 target)
    {
        _departureStart = Position;
        _departureTarget = target;
        _phaseElapsed = 0.0f;
        Phase = ExtractionAircraftPhase.Departing;
        _ramp.Rotation = Vector3.Zero;
        _dust.Visible = true;
        SetProcess(true);
    }

    private void AdvanceFlight(float delta)
    {
        if (Phase == ExtractionAircraftPhase.Hidden)
        {
            return;
        }

        _phaseElapsed += delta;
        _leftRotor.RotateY(delta * 31.0f);
        _rightRotor.RotateY(-delta * 31.0f);
        AnimateDust();

        switch (Phase)
        {
            case ExtractionAircraftPhase.Inbound:
                UpdateInbound();
                break;
            case ExtractionAircraftPhase.Boarding:
                UpdateBoarding();
                break;
            case ExtractionAircraftPhase.Departing:
                UpdateDeparture();
                break;
        }
    }

    private void UpdateInbound()
    {
        var t = Mathf.Clamp(_phaseElapsed / ArrivalDuration, 0.0f, 1.0f);
        var eased = t * t * (3.0f - 2.0f * t);
        var position = _startPosition.Lerp(BoardingPosition(), eased);
        position.Y += Mathf.Sin(t * Mathf.Pi) * 7.5f;
        Position = position;
        Rotation = new Vector3(
            Mathf.Lerp(0.08f, 0.0f, eased),
            Mathf.LerpAngle(-2.22f, 0.0f, eased),
            Mathf.Lerp(-0.1f, 0.0f, eased));
        _dust.Visible = t > 0.68f;
        _ramp.Rotation = new Vector3(Mathf.Lerp(0.0f, -0.58f, Mathf.Clamp((t - 0.78f) / 0.22f, 0.0f, 1.0f)), 0, 0);

        if (t >= 1.0f)
        {
            Phase = ExtractionAircraftPhase.Boarding;
            _phaseElapsed = 0.0f;
        }
    }

    private void UpdateBoarding()
    {
        Position = BoardingPosition() + Vector3.Up * (Mathf.Sin(_phaseElapsed * 2.1f) * 0.055f);
        Rotation = new Vector3(0.0f, Mathf.Sin(_phaseElapsed * 0.8f) * 0.008f, 0.0f);
        _ramp.Rotation = new Vector3(-0.58f, 0.0f, 0.0f);
        _dust.Visible = true;
    }

    private void UpdateDeparture()
    {
        const float duration = 3.8f;
        var t = Mathf.Clamp(_phaseElapsed / duration, 0.0f, 1.0f);
        var eased = t * t * (3.0f - 2.0f * t);
        Position = _departureStart.Lerp(_departureTarget, eased) + Vector3.Up * (Mathf.Sin(t * Mathf.Pi) * 8.0f);
        Rotation = new Vector3(
            Mathf.Lerp(0.0f, -0.08f, eased),
            Mathf.LerpAngle(0.0f, 2.28f, eased),
            Mathf.Lerp(0.0f, 0.12f, eased));
        _dust.Visible = t < 0.38f;
        if (t < 1.0f)
        {
            return;
        }

        Phase = ExtractionAircraftPhase.Hidden;
        Visible = false;
        _rotorAudio.Stop();
        SetProcess(false);
    }

    private Vector3 BoardingPosition() => PadPosition + new Vector3(0.0f, 2.45f, -4.1f);

    private void AnimateDust()
    {
        if (!_dust.Visible)
        {
            return;
        }
        for (var i = 0; i < _dust.GetChildCount(); i++)
        {
            if (_dust.GetChild(i) is not MeshInstance3D ring)
            {
                continue;
            }
            var cycle = Mathf.PosMod(_phaseElapsed * 0.72f + i * 0.31f, 1.0f);
            ring.Scale = Vector3.One * Mathf.Lerp(0.42f, 1.48f, cycle);
            ring.Transparency = Mathf.Lerp(0.42f, 1.0f, cycle);
        }
    }

    private void BuildVisuals()
    {
        _visual = new Node3D { Name = "RescueTiltRotorVisual" };
        AddChild(_visual);

        var charcoal = Material(new Color(0.055f, 0.07f, 0.068f), 0.68f, 0.38f);
        var steel = Material(new Color(0.31f, 0.35f, 0.32f), 0.74f, 0.34f);
        var glass = Material(new Color(0.035f, 0.14f, 0.16f), 0.82f, 0.12f);
        var rescue = Material(new Color(0.12f, 0.72f, 0.43f), 0.3f, 0.32f, new Color(0.04f, 0.42f, 0.19f));
        var warm = Material(new Color(1.0f, 0.58f, 0.16f), 0.12f, 0.28f, new Color(1.0f, 0.23f, 0.04f));

        Part(_visual, new CapsuleMesh { Radius = 0.82f, Height = 7.8f, RadialSegments = 24, Rings = 10 },
            new Vector3(0, 0, 0.15f), new Vector3(Mathf.Pi / 2.0f, 0, 0), steel);
        MeshBox(_visual, new Vector3(0, -0.05f, 0.2f), new Vector3(2.65f, 1.35f, 5.8f), steel);
        MeshBox(_visual, new Vector3(0, 0.16f, -3.25f), new Vector3(2.2f, 0.75f, 1.35f), glass, new Vector3(-0.18f, 0, 0));
        MeshBox(_visual, new Vector3(0, 0.42f, -0.4f), new Vector3(8.7f, 0.18f, 2.45f), charcoal);
        MeshBox(_visual, new Vector3(0, 0.68f, 3.4f), new Vector3(3.8f, 0.14f, 1.35f), charcoal);
        MeshBox(_visual, new Vector3(0, 1.25f, 3.68f), new Vector3(0.18f, 1.75f, 1.25f), steel, new Vector3(-0.18f, 0, 0));
        MeshBox(_visual, new Vector3(0, 0.62f, -0.2f), new Vector3(2.78f, 0.11f, 4.8f), rescue);

        _leftRotor = BuildRotor(_visual, new Vector3(-3.55f, 0.7f, -0.5f), charcoal, steel, warm);
        _rightRotor = BuildRotor(_visual, new Vector3(3.55f, 0.7f, -0.5f), charcoal, steel, warm);

        foreach (var x in new[] { -0.82f, 0.82f })
        {
            MeshBox(_visual, new Vector3(x, -1.02f, -1.55f), new Vector3(0.13f, 0.92f, 0.13f), charcoal, new Vector3(0.18f, 0, 0));
            MeshBox(_visual, new Vector3(x, -1.45f, -1.72f), new Vector3(0.24f, 0.12f, 0.58f), charcoal);
        }

        var doorway = Material(new Color(0.012f, 0.018f, 0.017f), 0.05f, 0.96f);
        MeshBox(_visual, new Vector3(0, -0.12f, 3.74f), new Vector3(1.62f, 1.18f, 0.08f), doorway);
        _ramp = new Node3D { Name = "BoardingRamp", Position = new Vector3(0, -0.68f, 3.72f) };
        _visual.AddChild(_ramp);
        MeshBox(_ramp, new Vector3(0, 0, 1.05f), new Vector3(1.55f, 0.12f, 2.1f), charcoal);
        for (var z = 0.25f; z <= 1.85f; z += 0.4f)
        {
            MeshBox(_ramp, new Vector3(0, -0.07f, z), new Vector3(1.42f, 0.04f, 0.06f), rescue);
        }

        _visual.AddChild(new OmniLight3D
        {
            Name = "BoardingLight",
            Position = new Vector3(0, -0.15f, 4.0f),
            LightColor = new Color(0.2f, 1.0f, 0.56f),
            LightEnergy = 5.0f,
            OmniRange = 10.0f,
            ShadowEnabled = false
        });
        _visual.AddChild(new SpotLight3D
        {
            Name = "ApproachLight",
            Position = new Vector3(0, -0.28f, -3.9f),
            RotationDegrees = new Vector3(-68.0f, 0, 0),
            LightColor = new Color(1.0f, 0.83f, 0.55f),
            LightEnergy = 8.0f,
            SpotRange = 32.0f,
            SpotAngle = 42.0f,
            ShadowEnabled = false
        });
        _visual.AddChild(new Label3D
        {
            Name = "RescueCallsign",
            Position = new Vector3(0, 0.46f, 2.55f),
            Text = "RESCUE 07  /  FRIENDLY",
            FontSize = 14,
            OutlineSize = 4,
            Modulate = new Color(0.42f, 1.0f, 0.68f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
            VisibilityRangeEnd = 95.0f,
            VisibilityRangeEndMargin = 12.0f
        });

        _dust = new Node3D { Name = "RotorWash", Position = new Vector3(0, -2.25f, 0) };
        _visual.AddChild(_dust);
        var dustMaterial = Material(new Color(0.55f, 0.52f, 0.43f, 0.22f), 0.0f, 1.0f);
        dustMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        dustMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        for (var i = 0; i < 3; i++)
        {
            _dust.AddChild(new MeshInstance3D
            {
                Name = $"RotorWashRing_{i}",
                Mesh = new TorusMesh { InnerRadius = 3.0f + i * 0.8f, OuterRadius = 3.16f + i * 0.8f, Rings = 36, RingSegments = 8 },
                MaterialOverride = dustMaterial
            });
        }

        _rotorAudio = new AudioStreamPlayer3D
        {
            Name = "ExtractionRotorAudio",
            Stream = SoundLab.ExtractionRotorLoop(),
            VolumeDb = -5.0f,
            MaxDistance = 145.0f
        };
        AddChild(_rotorAudio);
    }

    private static Node3D BuildRotor(
        Node3D parent,
        Vector3 position,
        StandardMaterial3D charcoal,
        StandardMaterial3D steel,
        StandardMaterial3D warm)
    {
        MeshBox(parent, position - Vector3.Up * 0.18f, new Vector3(1.05f, 1.0f, 1.25f), charcoal);
        var rotor = new Node3D { Position = position, Name = position.X < 0 ? "LeftRotor" : "RightRotor" };
        parent.AddChild(rotor);
        MeshBox(rotor, Vector3.Zero, new Vector3(7.4f, 0.055f, 0.18f), steel);
        MeshBox(rotor, Vector3.Zero, new Vector3(0.18f, 0.055f, 7.4f), steel);
        Part(rotor, new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.18f, Height = 0.24f, RadialSegments = 16 },
            Vector3.Zero, Vector3.Zero, warm);
        return rotor;
    }

    private static StandardMaterial3D Material(
        Color color,
        float metallic,
        float roughness,
        Color emission = default)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = metallic,
            Roughness = roughness
        };
        if (emission != default)
        {
            material.EmissionEnabled = true;
            material.Emission = emission;
            material.EmissionEnergyMultiplier = 2.2f;
        }
        return material;
    }

    private static MeshInstance3D MeshBox(
        Node3D parent,
        Vector3 position,
        Vector3 size,
        Godot.Material material,
        Vector3 rotation = default)
    {
        return Part(parent, new BoxMesh { Size = size }, position, rotation, material);
    }

    private static MeshInstance3D Part(
        Node3D parent,
        Mesh mesh,
        Vector3 position,
        Vector3 rotation,
        Godot.Material material)
    {
        var instance = new MeshInstance3D
        {
            Position = position,
            Rotation = rotation,
            Mesh = mesh,
            MaterialOverride = material
        };
        parent.AddChild(instance);
        return instance;
    }
}
