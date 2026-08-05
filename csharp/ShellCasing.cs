using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class ShellCasing : RigidBody3D
{
    private float _life = 4.0f;
    private bool _hasBounced;
    private AudioStreamPlayer3D _impactAudio = null!;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _rng.Randomize();
        Mass = 0.012f;
        GravityScale = 1.0f;
        CollisionLayer = 4;
        CollisionMask = 1;
        ContactMonitor = true;
        MaxContactsReported = 1;

        AddChild(new CollisionShape3D
        {
            Shape = new CylinderShape3D { Radius = 0.012f, Height = 0.052f }
        });

        var mesh = new CylinderMesh
        {
            TopRadius = 0.011f,
            BottomRadius = 0.014f,
            Height = 0.052f,
            RadialSegments = 8
        };
        AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.52f, 0.34f, 0.08f),
                Metallic = 0.95f,
                Roughness = 0.24f
            }
        });

        _impactAudio = new AudioStreamPlayer3D
        {
            Stream = SoundLab.CasingDrop(),
            VolumeDb = -22.0f,
            MaxDistance = 8.0f
        };
        AddChild(_impactAudio);
        BodyEntered += OnBodyEntered;
    }

    public void Launch(Vector3 impulseVelocity)
    {
        LinearVelocity = impulseVelocity;
        AngularVelocity = new Vector3(18.0f, 9.0f, 14.0f);
    }

    public override void _PhysicsProcess(double delta)
    {
        _life -= (float)delta;
        if (_life <= 0.0f)
        {
            QueueFree();
        }
    }

    private void OnBodyEntered(Node _)
    {
        if (_hasBounced)
        {
            return;
        }
        _hasBounced = true;
        _impactAudio.PitchScale = _rng.RandfRange(0.88f, 1.12f);
        _impactAudio.Play();
    }
}
