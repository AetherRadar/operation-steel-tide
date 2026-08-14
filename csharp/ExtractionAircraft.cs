using Godot;

namespace OperationSteelTide;

public enum ExtractionAircraftPhase
{
    Hidden,
    Inbound,
    Boarding,
    Departing,
    Arrived
}

[GlobalClass]
public partial class ExtractionAircraft : Node3D
{
    public const float ArrivalDuration = 6.4f;
    public const float TransferDuration = 9.2f;

    public Vector3 PadPosition { get; set; }
    public ExtractionAircraftPhase Phase { get; private set; } = ExtractionAircraftPhase.Hidden;
    public bool BoardingReady => Phase == ExtractionAircraftPhase.Boarding;
    public bool DestinationReached => Phase == ExtractionAircraftPhase.Arrived;
    public Camera3D CinematicCamera => _cinematicCamera;
    public Marker3D PlayerSeat => _passengerSeats[0];
    public int PassengerSeatCount => _passengerSeats.Length;
    public bool PlayerPassengerVisible => IsInstanceValid(_playerPassengerAvatar) && _playerPassengerAvatar.Visible;
    public bool UsesAuthoredVisual { get; private set; }
    public float ArrivalProgress => Phase == ExtractionAircraftPhase.Inbound
        ? Mathf.Clamp(_phaseElapsed / ArrivalDuration, 0.0f, 1.0f)
        : Phase == ExtractionAircraftPhase.Hidden ? 0.0f : 1.0f;

    private Node3D _visual = null!;
    private Node3D _leftRotor = null!;
    private Node3D _rightRotor = null!;
    private Node3D _ramp = null!;
    private Node3D _dust = null!;
    private Node3D _cabinVisual = null!;
    private Camera3D _cinematicCamera = null!;
    private readonly Marker3D[] _passengerSeats = new Marker3D[3];
    private Node3D _playerPassengerAvatar = null!;
    private StandardMaterial3D _playerPassengerMaterial = null!;
    private AudioStreamPlayer3D _rotorAudio = null!;
    private Vector3 _startPosition;
    private Vector3 _departureStart;
    private Vector3 _departureTarget;
    private float _phaseElapsed;
    private bool _landAtDepartureTarget;

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
        _visual.Visible = true;
        _cabinVisual.Visible = false;
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
        BeginDeparture(PadPosition + new Vector3(72.0f, 32.0f, 66.0f), false);
    }

    public void BeginDeparture()
    {
        BeginDeparture(PadPosition + new Vector3(-96.0f, 42.0f, -78.0f), false);
    }

    public void BeginTransferTo(Vector3 destination)
    {
        BeginDeparture(destination, true);
    }

    public Marker3D SquadSeat(int squadIndex)
    {
        return _passengerSeats[Mathf.Clamp(squadIndex + 1, 1, _passengerSeats.Length - 1)];
    }

    public void ShowPlayerPassenger(Color accent)
    {
        _playerPassengerMaterial.AlbedoColor = accent.Darkened(0.34f);
        _playerPassengerMaterial.Emission = accent.Darkened(0.72f);
        _playerPassengerAvatar.Visible = true;
    }

    public void ForceBoardingReadyForValidation()
    {
        Visible = true;
        _visual.Visible = true;
        _cabinVisual.Visible = false;
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

    public void AdvanceForValidation(float delta)
    {
        AdvanceFlight(Mathf.Max(0.0f, delta));
    }

    private void BeginDeparture(Vector3 target, bool landAtTarget)
    {
        _departureStart = Position;
        _departureTarget = target;
        _landAtDepartureTarget = landAtTarget;
        _phaseElapsed = 0.0f;
        Phase = ExtractionAircraftPhase.Departing;
        // Keep the authored aircraft visible during the entire transfer. The
        // cinematic camera is mounted outside the fuselage, so hiding the
        // aircraft here would turn the flight shot into an empty cabin view.
        _visual.Visible = true;
        _cabinVisual.Visible = false;
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
        var duration = _landAtDepartureTarget ? TransferDuration : 3.8f;
        var t = Mathf.Clamp(_phaseElapsed / duration, 0.0f, 1.0f);
        var eased = t * t * (3.0f - 2.0f * t);
        var route = _departureTarget - _departureStart;
        var cruiseLift = _landAtDepartureTarget
            ? Mathf.Clamp(new Vector2(route.X, route.Z).Length() * 0.12f, 58.0f, 82.0f)
            : 8.0f;
        Position = _departureStart.Lerp(_departureTarget, eased)
            + Vector3.Up * (Mathf.Sin(t * Mathf.Pi) * cruiseLift);
        var routeYaw = route.LengthSquared() > 0.001f
            ? Mathf.Atan2(-route.X, -route.Z)
            : 0.0f;
        var landingBlend = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp((t - 0.78f) / 0.22f, 0.0f, 1.0f));
        var yaw = _landAtDepartureTarget
            ? Mathf.LerpAngle(routeYaw, 0.0f, landingBlend)
            : Mathf.LerpAngle(0.0f, 2.28f, eased);
        Rotation = new Vector3(
            -Mathf.Sin(t * Mathf.Pi) * 0.08f,
            yaw,
            Mathf.Sin(t * Mathf.Pi) * 0.08f);
        _dust.Visible = t < 0.22f || (_landAtDepartureTarget && t > 0.82f);
        if (t < 1.0f)
        {
            return;
        }

        Position = _departureTarget;
        Rotation = Vector3.Zero;
        if (_landAtDepartureTarget)
        {
            Phase = ExtractionAircraftPhase.Arrived;
            _ramp.Rotation = new Vector3(-0.58f, 0.0f, 0.0f);
            _visual.Visible = true;
            _cabinVisual.Visible = false;
            _dust.Visible = true;
            _rotorAudio.Stop();
            SetProcess(false);
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
        var charcoal = Material(new Color(0.055f, 0.07f, 0.068f), 0.68f, 0.38f);
        var steel = Material(new Color(0.31f, 0.35f, 0.32f), 0.74f, 0.34f);
        var glass = Material(new Color(0.035f, 0.14f, 0.16f), 0.82f, 0.12f);
        var rescue = Material(new Color(0.12f, 0.72f, 0.43f), 0.3f, 0.32f, new Color(0.04f, 0.42f, 0.19f));
        var warm = Material(new Color(1.0f, 0.58f, 0.16f), 0.12f, 0.28f, new Color(1.0f, 0.23f, 0.04f));

        var authoredRig = ExtractionAircraftVisualRig.TryInstantiate();
        if (authoredRig is not null)
        {
            _visual = authoredRig.Root;
            _leftRotor = authoredRig.LeftRotor;
            _rightRotor = authoredRig.RightRotor;
            _ramp = authoredRig.BoardingDoor;
            UsesAuthoredVisual = true;
            AddChild(_visual);
        }
        else
        {
            _visual = new Node3D { Name = "RescueTiltRotorVisual" };
            AddChild(_visual);
            Part(_visual, new CapsuleMesh { Radius = 0.82f, Height = 7.8f, RadialSegments = 24, Rings = 10 },
                new Vector3(0, 0, 0.15f), new Vector3(Mathf.Pi / 2.0f, 0, 0), steel);
            MeshBox(_visual, new Vector3(0, -0.05f, 0.2f), new Vector3(2.65f, 1.35f, 5.8f), steel);
            MeshBox(_visual, new Vector3(0, 0.16f, -3.25f), new Vector3(2.2f, 0.75f, 1.35f), glass, new Vector3(-0.18f, 0, 0));
            MeshBox(_visual, new Vector3(0, 0.42f, -0.4f), new Vector3(8.7f, 0.18f, 2.45f), charcoal);
            MeshBox(_visual, new Vector3(0, 0.68f, 3.4f), new Vector3(3.8f, 0.14f, 1.35f), charcoal);
            MeshBox(_visual, new Vector3(0, 1.25f, 3.68f), new Vector3(0.18f, 1.75f, 1.25f), steel, new Vector3(-0.18f, 0, 0));
            MeshBox(_visual, new Vector3(0, 1.12f, -0.2f), new Vector3(2.78f, 0.11f, 4.8f), rescue);
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
        BuildPassengerCabin(charcoal, steel, rescue);
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

    private void BuildPassengerCabin(
        StandardMaterial3D charcoal,
        StandardMaterial3D steel,
        StandardMaterial3D rescue)
    {
        _cabinVisual = new Node3D { Name = "PassengerCabinVisual", Visible = false };
        AddChild(_cabinVisual);
        var seatPositions = new[]
        {
            new Vector3(0.7f, -0.78f, -0.35f),
            new Vector3(-0.7f, -0.78f, -0.35f),
            new Vector3(0.0f, -0.78f, -0.35f)
        };
        for (var i = 0; i < seatPositions.Length; i++)
        {
            var seat = new Marker3D
            {
                Name = i == 0 ? "PlayerPassengerSeat" : $"SquadPassengerSeat_{i}",
                Position = seatPositions[i],
                Rotation = new Vector3(0.0f, Mathf.Pi, 0.0f)
            };
            _cabinVisual.AddChild(seat);
            _passengerSeats[i] = seat;
            MeshBox(_cabinVisual, seatPositions[i] + new Vector3(0, -0.12f, 0.08f), new Vector3(0.56f, 0.12f, 0.52f), charcoal);
            MeshBox(_cabinVisual, seatPositions[i] + new Vector3(0, 0.35f, -0.3f), new Vector3(0.56f, 0.82f, 0.1f), charcoal);
        }

        var cabin = Material(new Color(0.035f, 0.052f, 0.05f), 0.5f, 0.48f);
        cabin.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        MeshBox(_cabinVisual, new Vector3(0, -1.02f, 0.35f), new Vector3(2.35f, 0.1f, 4.0f), cabin);
        MeshBox(_cabinVisual, new Vector3(0, 1.0f, 0.25f), new Vector3(2.35f, 0.08f, 3.8f), cabin);
        MeshBox(_cabinVisual, new Vector3(0, 0.0f, -1.62f), new Vector3(2.3f, 1.95f, 0.08f), cabin);
        for (var z = -1.15f; z <= 1.55f; z += 0.9f)
        {
            MeshBox(_cabinVisual, new Vector3(-1.13f, 0.0f, z), new Vector3(0.08f, 1.85f, 0.45f), cabin);
            MeshBox(_cabinVisual, new Vector3(1.13f, 0.0f, z), new Vector3(0.08f, 1.85f, 0.45f), cabin);
        }
        _cabinVisual.AddChild(new OmniLight3D
        {
            Name = "CabinLight",
            Position = new Vector3(0.0f, 0.72f, 0.5f),
            LightColor = new Color(0.35f, 1.0f, 0.68f),
            LightEnergy = 0.9f,
            OmniRange = 4.5f,
            ShadowEnabled = false
        });

        _playerPassengerMaterial = Material(new Color(0.08f, 0.42f, 0.28f), 0.18f, 0.62f, new Color(0.01f, 0.08f, 0.05f));
        _playerPassengerAvatar = new Node3D
        {
            Name = "PlayerPassengerAvatar",
            Position = new Vector3(0.0f, 0.52f, 0.02f),
            Visible = false
        };
        _passengerSeats[0].AddChild(_playerPassengerAvatar);
        MeshBox(_playerPassengerAvatar, new Vector3(0, 0.22f, 0), new Vector3(0.5f, 0.75f, 0.32f), _playerPassengerMaterial);
        Part(
            _playerPassengerAvatar,
            new SphereMesh { Radius = 0.22f, Height = 0.44f, RadialSegments = 12, Rings = 6 },
            new Vector3(0, 0.78f, 0),
            Vector3.Zero,
            steel);
        MeshBox(_playerPassengerAvatar, new Vector3(-0.34f, 0.18f, 0.04f), new Vector3(0.16f, 0.68f, 0.16f), rescue, new Vector3(0, 0, -0.42f));
        MeshBox(_playerPassengerAvatar, new Vector3(0.34f, 0.18f, 0.04f), new Vector3(0.16f, 0.68f, 0.16f), rescue, new Vector3(0, 0, 0.42f));

        _cinematicCamera = new Camera3D
        {
            Name = "ExtractionCinematicCamera",
            // Exterior chase framing: the aircraft remains readable during
            // the whole flight instead of showing the inside of the cabin.
            Position = new Vector3(8.8f, 4.75f, 12.6f),
            Fov = 58.0f,
            Near = 0.05f,
            Far = 1400.0f,
            Current = false
        };
        AddChild(_cinematicCamera);
        _cinematicCamera.LookAt(new Vector3(0.0f, 0.28f, 0.15f), Vector3.Up);
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
