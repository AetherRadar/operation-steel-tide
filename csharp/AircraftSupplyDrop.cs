using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class AircraftSupplyDrop : StaticBody3D, ILootSource, IOpenableLootSource
{
    public List<LootItem> Loot { get; } = new();
    public Node3D LootNode => this;
    public bool IsSearchable => true;
    public float SearchDuration => 1.15f;
    public bool GroundResolved { get; private set; }
    public bool HasBeacon => IsInstanceValid(_beaconLight) && IsInstanceValid(_beaconBeam);
    public int VisualPartCount { get; private set; }
    public bool IsOpened { get; private set; }

    private Node3D _lid = null!;
    private Label3D _label = null!;
    private OmniLight3D _beaconLight = null!;
    private MeshInstance3D _beaconBeam = null!;
    private Tween? _beaconTween;
    private string _language = "en";
    private int _partCounter;

    public void Configure(IEnumerable<LootItem> loot, string language, bool groundResolved)
    {
        Loot.Clear();
        Loot.AddRange(loot);
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        GroundResolved = groundResolved;
    }

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 0;
        AddToGroup("aircraft_supply_drops");
        AddChild(new CollisionShape3D
        {
            Name = "SupplyDropCollision",
            Position = new Vector3(0.0f, 0.52f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(1.86f, 1.04f, 1.12f) }
        });
        BuildCrate();
        BuildBeacon();
    }

    public override void _ExitTree()
    {
        _beaconTween?.Kill();
        _beaconTween = null;
    }

    public string DisplayName(string language)
    {
        return GameLocalization.Get("aircraft_supply_drop", language, "Aircraft supply drop");
    }

    public void SetLanguage(string language)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        if (IsInstanceValid(_label))
        {
            _label.Text = BeaconLabel();
        }
    }

    public void OnSearched()
    {
        if (IsOpened)
        {
            return;
        }
        IsOpened = true;
        CreateTween()
            .SetProcessMode(Tween.TweenProcessMode.Idle)
            .SetPauseMode(Tween.TweenPauseMode.Process)
            .TweenProperty(_lid, "rotation:x", -1.25f, 0.42f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
    }

    private void BuildCrate()
    {
        var shell = Material(new Color(0.14f, 0.2f, 0.13f), 0.42f, 0.72f);
        var steel = Material(new Color(0.12f, 0.14f, 0.14f), 0.8f, 0.28f);
        var accent = Material(new Color(0.95f, 0.43f, 0.08f), 0.18f, 0.38f, true);
        var fabric = Material(new Color(0.62f, 0.66f, 0.58f), 0.02f, 0.94f);

        Part(this, new BoxMesh { Size = new Vector3(1.78f, 0.82f, 1.04f) }, new Vector3(0, 0.43f, 0), shell);
        _lid = new Node3D
        {
            Name = "SupplyDropLid",
            Position = new Vector3(0, 0.87f, 0.52f)
        };
        AddChild(_lid);
        Part(
            _lid,
            new BoxMesh { Size = new Vector3(1.82f, 0.14f, 1.08f) },
            new Vector3(0, 0, -0.52f),
            shell);

        foreach (var x in new[] { -0.83f, 0.83f })
        {
            foreach (var z in new[] { -0.46f, 0.46f })
            {
                Part(this, new BoxMesh { Size = new Vector3(0.12f, 0.92f, 0.12f) }, new Vector3(x, 0.48f, z), steel);
            }
        }
        foreach (var x in new[] { -0.53f, 0.53f })
        {
            Part(this, new BoxMesh { Size = new Vector3(0.09f, 0.1f, 1.1f) }, new Vector3(x, 0.74f, 0), accent);
        }
        Part(this, new BoxMesh { Size = new Vector3(0.72f, 0.23f, 0.55f) }, new Vector3(0, 1.05f, 0.02f), fabric);
        Part(this, new BoxMesh { Size = new Vector3(0.1f, 0.27f, 0.59f) }, new Vector3(-0.22f, 1.06f, 0.02f), accent);
        Part(this, new BoxMesh { Size = new Vector3(0.1f, 0.27f, 0.59f) }, new Vector3(0.22f, 1.06f, 0.02f), accent);

        _label = new Label3D
        {
            Name = "SupplyDropLabel",
            Position = new Vector3(0, 2.0f, 0),
            Text = BeaconLabel(),
            FontSize = 20,
            OutlineSize = 7,
            Modulate = new Color(1.0f, 0.62f, 0.2f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 145.0f,
            VisibilityRangeEndMargin = 18.0f
        };
        AddChild(_label);
    }

    private void BuildBeacon()
    {
        var beaconMaterial = Material(new Color(1.0f, 0.34f, 0.04f, 0.3f), 0.0f, 0.2f, true, true);
        _beaconBeam = Part(
            this,
            new CylinderMesh
            {
                TopRadius = 0.055f,
                BottomRadius = 0.12f,
                Height = 22.0f,
                RadialSegments = 12
            },
            new Vector3(0, 11.6f, 0),
            beaconMaterial);
        _beaconBeam.Name = "SupplyDropBeaconBeam";
        _beaconLight = new OmniLight3D
        {
            Name = "SupplyDropBeaconLight",
            Position = new Vector3(0, 1.35f, 0),
            LightColor = new Color(1.0f, 0.34f, 0.06f),
            LightEnergy = 3.8f,
            OmniRange = 13.0f,
            ShadowEnabled = false
        };
        AddChild(_beaconLight);
        _beaconTween = CreateTween().SetLoops();
        _beaconTween.TweenProperty(_beaconLight, "light_energy", 6.2f, 0.55f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        _beaconTween.TweenProperty(_beaconLight, "light_energy", 2.8f, 0.55f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }

    private string BeaconLabel()
    {
        return GameLocalization.Get("aircraft_supply_label", _language, "AIR DROP  //  SUPPLIES");
    }

    private MeshInstance3D Part(
        Node parent,
        PrimitiveMesh mesh,
        Vector3 position,
        Godot.Material material)
    {
        var part = new MeshInstance3D
        {
            Name = $"SupplyDropPart_{_partCounter++:00}",
            Position = position,
            Mesh = mesh,
            MaterialOverride = material
        };
        parent.AddChild(part);
        VisualPartCount++;
        return part;
    }

    private static StandardMaterial3D Material(
        Color color,
        float metallic,
        float roughness,
        bool emission = false,
        bool transparent = false)
    {
        return new StandardMaterial3D
        {
            Transparency = transparent ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
            ShadingMode = transparent ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
            AlbedoColor = color,
            Metallic = metallic,
            Roughness = roughness,
            EmissionEnabled = emission,
            Emission = emission ? new Color(color.R, color.G, color.B) : Colors.Black,
            EmissionEnergyMultiplier = emission ? 2.4f : 1.0f
        };
    }
}
