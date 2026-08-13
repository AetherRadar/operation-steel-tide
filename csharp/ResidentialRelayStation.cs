using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public enum ResidentialRelayKind
{
    Medical,
    Security,
    Utility,
    Evacuation
}

/// <summary>
/// A compact residential service station that turns the former blank annexes into
/// a readable risk/reward route: approach the terminal, climb the marked ladder,
/// and claim the roof cache after the signal is brought online.
/// </summary>
[GlobalClass]
public partial class ResidentialRelayStation : StaticBody3D
{
    private static readonly Dictionary<Vector3, BoxMesh> SharedBoxes = new();

    internal static void ReleaseSharedResources()
    {
        SharedBoxes.Clear();
    }

    private Godot.Material _shell = null!;
    private Godot.Material _trim = null!;
    private Godot.Material _utility = null!;
    private Color _towerAccent;
    private StandardMaterial3D _screenMaterial = null!;
    private StandardMaterial3D _statusMaterial = null!;
    private MeshInstance3D _statusLamp = null!;
    private Label3D _instructionLabel = null!;
    private Label3D _roofLabel = null!;
    private OmniLight3D _statusLight = null!;
    private string _language = "en";
    private bool _configured;
    private float _pulseTime;
    private int _partCounter;
    private int _batchCounter;

    public ResidentialRelayKind Kind { get; private set; }
    public int TowerIndex { get; private set; }
    public int CornerIndex { get; private set; }
    public int FrontSign { get; private set; } = 1;
    public bool IsActivated { get; private set; }
    public bool IsActivating { get; private set; }
    public float ActivationProgress { get; private set; }
    public bool CacheUnlocked { get; private set; }
    public int LadderRungCount { get; private set; }
    public bool HasRoofCollision { get; private set; }
    public Color Accent => KindAccent(Kind, _towerAccent);
    public float ActivationDuration => 3.4f;
    public string EnglishLabel => Kind switch
    {
        ResidentialRelayKind.Medical => "MEDICAL RELAY",
        ResidentialRelayKind.Security => "SECURITY RELAY",
        ResidentialRelayKind.Utility => "UTILITY RELAY",
        _ => "EVAC RELAY"
    };

    public Vector3 LadderApproachPoint => ToGlobal(new Vector3(0.56f, -1.02f, FrontSign * 1.58f));
    public Vector3 TerminalApproachPoint => ToGlobal(new Vector3(-0.56f, -1.02f, FrontSign * 1.52f));
    public Vector3 RoofLandingPoint => ToGlobal(new Vector3(0.56f, 1.46f, FrontSign * 0.68f));
    public Vector3 RoofCachePoint => ToGlobal(new Vector3(-0.54f, 1.54f, -FrontSign * 0.42f));

    public void Configure(
        ResidentialRelayKind kind,
        int towerIndex,
        int cornerIndex,
        int frontSign,
        Color towerAccent,
        Godot.Material shell,
        Godot.Material trim,
        Godot.Material utility)
    {
        Kind = kind;
        TowerIndex = towerIndex;
        CornerIndex = cornerIndex;
        FrontSign = frontSign < 0 ? -1 : 1;
        _towerAccent = towerAccent;
        _shell = shell;
        _trim = trim;
        _utility = utility;
        _configured = true;
    }

    public void SetLanguage(string language)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        RefreshLabels();
    }

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 0;
        if (_configured)
        {
            BuildStation();
        }
        AddToGroup("residential_relay_stations");
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_statusLamp))
        {
            return;
        }
        _pulseTime += (float)delta;
        var pulse = IsActivated
            ? 1.0f + Mathf.Sin(_pulseTime * 3.2f) * 0.08f
            : 0.94f + Mathf.Sin(_pulseTime * 5.0f) * 0.12f;
        _statusLamp.Scale = Vector3.One * pulse;
        if (IsInstanceValid(_statusLight))
        {
            _statusLight.LightEnergy = IsActivated ? 1.8f + pulse * 0.55f : 0.85f + pulse * 0.45f;
        }
    }

    public void SetActivationProgress(float progress)
    {
        if (IsActivated)
        {
            return;
        }
        ActivationProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
        IsActivating = ActivationProgress > 0.001f;
        if (IsInstanceValid(_screenMaterial))
        {
            var color = IsActivating ? new Color(1.0f, 0.62f, 0.2f) : new Color(0.12f, 0.5f, 0.62f);
            _screenMaterial.AlbedoColor = color;
            _screenMaterial.Emission = color;
            _screenMaterial.EmissionEnergyMultiplier = IsActivating ? 2.1f : 1.25f;
        }
        RefreshLabels();
    }

    public void CancelActivation()
    {
        if (IsActivated)
        {
            return;
        }
        SetActivationProgress(0.0f);
    }

    public bool CompleteActivation()
    {
        if (IsActivated)
        {
            return false;
        }
        IsActivated = true;
        IsActivating = false;
        ActivationProgress = 1.0f;
        if (IsInstanceValid(_screenMaterial))
        {
            var color = new Color(0.22f, 0.95f, 0.66f);
            _screenMaterial.AlbedoColor = color;
            _screenMaterial.Emission = color;
            _screenMaterial.EmissionEnergyMultiplier = 1.9f;
        }
        if (IsInstanceValid(_statusMaterial))
        {
            _statusMaterial.AlbedoColor = new Color(0.22f, 0.95f, 0.66f);
            _statusMaterial.Emission = new Color(0.1f, 0.8f, 0.48f);
            _statusMaterial.EmissionEnergyMultiplier = 1.9f;
        }
        if (IsInstanceValid(_statusLight))
        {
            _statusLight.LightColor = new Color(0.22f, 1.0f, 0.62f);
        }
        if (IsInstanceValid(_instructionLabel))
        {
            _instructionLabel.Modulate = new Color(0.3f, 1.0f, 0.7f);
        }
        if (IsInstanceValid(_roofLabel))
        {
            _roofLabel.Modulate = new Color(0.3f, 1.0f, 0.7f);
        }
        RefreshLabels();
        return true;
    }

    public void UnlockCache()
    {
        CacheUnlocked = true;
        if (IsInstanceValid(_roofLabel))
        {
            _roofLabel.Visible = true;
        }
        RefreshLabels();
    }

    public bool IsNearLadder(Vector3 position, float range = 2.65f)
        => position.DistanceTo(LadderApproachPoint) <= range;

    public bool IsNearTerminal(Vector3 position, float range = 2.65f)
        => position.DistanceTo(TerminalApproachPoint) <= range;

    public bool IsOnRoof(Vector3 position)
    {
        var flat = position - RoofLandingPoint;
        flat.Y = 0.0f;
        return position.Y >= RoofLandingPoint.Y - 0.48f && flat.Length() <= 2.0f;
    }

    private void BuildStation()
    {
        var accent = Accent;
        var dark = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.025f, 0.035f, 0.035f),
            Metallic = 0.8f,
            Roughness = 0.3f
        };
        _screenMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.5f, 0.62f),
            EmissionEnabled = true,
            Emission = new Color(0.08f, 0.38f, 0.5f),
            EmissionEnergyMultiplier = 1.45f,
            Metallic = 0.2f,
            Roughness = 0.24f
        };
        _statusMaterial = new StandardMaterial3D
        {
            AlbedoColor = accent,
            EmissionEnabled = true,
            Emission = accent,
            EmissionEnergyMultiplier = 1.7f,
            Roughness = 0.3f
        };

        AddShape(this, "RelayBase", Vector3.Zero, new Vector3(2.35f, 2.36f, 2.35f));
        AddShape(this, "RelayRoof", new Vector3(0, 1.28f, 0), new Vector3(2.62f, 0.16f, 2.62f));
        HasRoofCollision = true;

        Part(this, Box(new Vector3(2.35f, 2.36f, 2.35f)), Vector3.Zero, _shell);
        Part(this, Box(new Vector3(2.58f, 0.16f, 2.58f)), new Vector3(0, 1.28f, 0), _trim);
        Part(this, Box(new Vector3(2.1f, 0.08f, 0.08f)), new Vector3(0, 1.4f, -FrontSign * 0.82f), dark);

        // Break up the otherwise blank equipment shell with a readable vent bank and
        // service bands. These remain mesh-only, so they cannot catch player capsules.
        Part(this, Box(new Vector3(0.045f, 1.0f, 1.2f)), new Vector3(1.19f, -0.15f, 0), _utility);
        var ventSlots = new List<Transform3D>(5);
        for (var slot = 0; slot < 5; slot++)
        {
            ventSlots.Add(new Transform3D(Basis.Identity, new Vector3(1.22f, -0.5f + slot * 0.18f, 0)));
        }
        Batch(this, new Vector3(0.055f, 0.065f, 0.92f), _trim, ventSlots);
        Part(this, Box(new Vector3(0.055f, 0.12f, 1.75f)), new Vector3(-1.2f, 0.42f, 0), _statusMaterial);

        // The ladder is deliberately offset from the console so the two actions read as
        // separate affordances even when the player approaches from a tight courtyard.
        var ladderZ = FrontSign * 1.29f;
        foreach (var x in new[] { 0.2f, 0.92f })
        {
            Part(this, Box(new Vector3(0.08f, 2.48f, 0.08f)), new Vector3(x, 0.05f, ladderZ), _trim);
        }
        var ladderRungs = new List<Transform3D>(8);
        for (var rung = 0; rung < 8; rung++)
        {
            var y = -0.94f + rung * 0.31f;
            ladderRungs.Add(new Transform3D(Basis.Identity, new Vector3(0.56f, y, ladderZ)));
        }
        Batch(this, new Vector3(0.9f, 0.07f, 0.08f), _statusMaterial, ladderRungs);
        LadderRungCount = ladderRungs.Count;
        Part(this, Box(new Vector3(1.18f, 0.07f, 0.08f)), new Vector3(0.56f, 1.38f, ladderZ), _statusMaterial);

        var consoleX = -0.56f;
        var consoleZ = FrontSign * 1.2f;
        Part(this, Box(new Vector3(1.12f, 0.82f, 0.34f)), new Vector3(consoleX, -0.34f, consoleZ), _utility);
        Part(this, Box(new Vector3(0.8f, 0.48f, 0.045f)), new Vector3(consoleX, -0.1f, FrontSign * 1.39f), _screenMaterial);
        Part(this, Box(new Vector3(0.16f, 0.08f, 0.05f)), new Vector3(consoleX + 0.34f, -0.5f, FrontSign * 1.39f), _statusMaterial);
        Part(this, Box(new Vector3(0.56f, 0.055f, 0.045f)), new Vector3(consoleX, -0.66f, FrontSign * 1.39f), dark);

        var poleX = -0.78f;
        Part(this, Box(new Vector3(0.08f, 0.75f, 0.08f)), new Vector3(poleX, 1.66f, 0), dark);
        Part(this, Box(new Vector3(0.78f, 0.07f, 0.07f)), new Vector3(poleX, 2.04f, 0), _statusMaterial);
        _statusLamp = Part(this, Box(new Vector3(0.2f, 0.2f, 0.2f)), new Vector3(poleX, 2.2f, 0), _statusMaterial);
        _statusLight = new OmniLight3D
        {
            Name = "RelayStatusLight",
            Position = new Vector3(poleX, 2.18f, 0),
            LightColor = accent,
            LightEnergy = 1.2f,
            OmniRange = 4.8f,
            ShadowEnabled = false,
            DistanceFadeEnabled = true,
            DistanceFadeBegin = 24.0f,
            DistanceFadeLength = 8.0f
        };
        AddChild(_statusLight);

        // Open front guard leaves a clear ladder landing; thin rails keep sightlines and
        // movement readable instead of surrounding the perch with solid black panels.
        AddGuardAlongZ(-1.18f, -1.18f, 1.18f, dark);
        AddGuardAlongZ(1.18f, -1.18f, 1.18f, dark);
        AddGuardAlongX(-FrontSign * 1.18f, -1.18f, 1.18f, dark);
        AddGuardAlongX(FrontSign * 1.18f, -1.18f, 0.02f, dark);

        _instructionLabel = new Label3D
        {
            Name = "RelayInstructionLabel",
            Position = new Vector3(consoleX, 0.42f, FrontSign * 1.44f),
            Text = InstructionLabelText(),
            FontSize = 13,
            OutlineSize = 5,
            Modulate = new Color(1.0f, 0.72f, 0.3f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 22.0f
        };
        _instructionLabel.AddToGroup("residential_localized_labels");
        AddChild(_instructionLabel);
        _roofLabel = new Label3D
        {
            Name = "RelayRoofLabel",
            Position = new Vector3(-0.54f, 2.04f, -FrontSign * 0.42f),
            Text = RoofLabelText(),
            FontSize = 12,
            OutlineSize = 5,
            Modulate = new Color(1.0f, 0.62f, 0.22f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 20.0f
        };
        _roofLabel.AddToGroup("residential_localized_labels");
        AddChild(_roofLabel);
    }

    private void RefreshLabels()
    {
        if (IsInstanceValid(_instructionLabel))
        {
            _instructionLabel.Text = InstructionLabelText();
        }
        if (IsInstanceValid(_roofLabel))
        {
            _roofLabel.Text = RoofLabelText();
        }
    }

    private string InstructionLabelText()
    {
        if (IsActivated)
        {
            return GameLocalization.Get("residential_relay_online", _language, "ONLINE // ROOF CACHE");
        }
        if (IsActivating)
        {
            return GameLocalization.Get("residential_relay_uplink", _language, "UPLINK // HOLD F");
        }
        return $"{RelayKindLabel()} // {GameLocalization.Get("residential_relay_hold", _language, "HOLD F")}";
    }

    private string RoofLabelText() => GameLocalization.Get(
        IsActivated || CacheUnlocked ? "residential_roof_cache_unlocked" : "residential_roof_cache_locked",
        _language,
        IsActivated || CacheUnlocked ? "ROOF CACHE // UNLOCKED" : "ROOF CACHE // LOCKED");

    private string RelayKindLabel()
    {
        var key = Kind switch
        {
            ResidentialRelayKind.Medical => "residential_relay_medical",
            ResidentialRelayKind.Security => "residential_relay_security",
            ResidentialRelayKind.Utility => "residential_relay_utility",
            _ => "residential_relay_evac"
        };
        return GameLocalization.Get(key, _language, EnglishLabel);
    }

    private void AddGuardAlongZ(float x, float start, float end, Godot.Material material)
    {
        var length = end - start;
        var center = (start + end) * 0.5f;
        var rails = new List<Transform3D>(2);
        foreach (var y in new[] { 1.63f, 1.98f })
        {
            rails.Add(new Transform3D(Basis.Identity, new Vector3(x, y, center)));
        }
        Batch(this, new Vector3(0.07f, 0.07f, length), material, rails);
        var posts = new List<Transform3D>(3);
        foreach (var z in new[] { start, center, end })
        {
            posts.Add(new Transform3D(Basis.Identity, new Vector3(x, 1.68f, z)));
        }
        Batch(this, new Vector3(0.07f, 0.72f, 0.07f), material, posts);
    }

    private void AddGuardAlongX(float z, float start, float end, Godot.Material material)
    {
        var length = end - start;
        var center = (start + end) * 0.5f;
        var rails = new List<Transform3D>(2);
        foreach (var y in new[] { 1.63f, 1.98f })
        {
            rails.Add(new Transform3D(Basis.Identity, new Vector3(center, y, z)));
        }
        Batch(this, new Vector3(length, 0.07f, 0.07f), material, rails);
        var posts = new List<Transform3D>(3);
        foreach (var x in new[] { start, center, end })
        {
            posts.Add(new Transform3D(Basis.Identity, new Vector3(x, 1.68f, z)));
        }
        Batch(this, new Vector3(0.07f, 0.72f, 0.07f), material, posts);
    }

    private static void AddShape(StaticBody3D body, string name, Vector3 position, Vector3 size)
    {
        body.AddChild(new CollisionShape3D
        {
            Name = name,
            Position = position,
            Shape = new BoxShape3D { Size = size }
        });
    }

    private static Color KindAccent(ResidentialRelayKind kind, Color fallback)
    {
        return kind switch
        {
            ResidentialRelayKind.Medical => new Color(0.2f, 0.9f, 0.58f),
            ResidentialRelayKind.Security => new Color(0.25f, 0.62f, 1.0f),
            ResidentialRelayKind.Utility => new Color(1.0f, 0.67f, 0.2f),
            ResidentialRelayKind.Evacuation => new Color(1.0f, 0.38f, 0.24f),
            _ => fallback
        };
    }

    private static BoxMesh Box(Vector3 size)
    {
        if (!SharedBoxes.TryGetValue(size, out var mesh))
        {
            mesh = new BoxMesh { Size = size };
            SharedBoxes[size] = mesh;
        }
        return mesh;
    }

    private MeshInstance3D Part(Node parent, PrimitiveMesh mesh, Vector3 position, Godot.Material material)
    {
        var part = new MeshInstance3D
        {
            Name = $"RelayPart_{_partCounter++:00}",
            Mesh = mesh,
            Position = position,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        parent.AddChild(part);
        part.AddToGroup("map_detail_visuals");
        return part;
    }

    private void Batch(
        Node parent,
        Vector3 size,
        Godot.Material material,
        IReadOnlyList<Transform3D> transforms)
    {
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = Box(size),
            InstanceCount = transforms.Count
        };
        for (var index = 0; index < transforms.Count; index++)
        {
            multiMesh.SetInstanceTransform(index, transforms[index]);
        }
        var batch = new MultiMeshInstance3D
        {
            Name = $"RelayBatch_{_batchCounter++:00}",
            Multimesh = multiMesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        parent.AddChild(batch);
        batch.AddToGroup("map_detail_visuals");
    }
}
