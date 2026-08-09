using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>World pickup with rarity glow. Tap F via nearby loot interaction or walk-over collect.</summary>
[GlobalClass]
public partial class GradedLootPickup : StaticBody3D, ILootSource
{
    public List<LootItem> Loot { get; } = new();
    public Node3D LootNode => this;
    public bool IsSearchable => Loot.Count > 0;
    public float SearchDuration => 0.55f;
    public LootGrade Grade { get; private set; } = LootGrade.Common;
    public string EnglishName { get; set; } = "Field cache";
    public string ChineseName { get; set; } = "战地物资";

    public bool VisualReady => IsInstanceValid(_core) && _core.Visible
        && IsInstanceValid(_glow) && _glow.Visible
        && IsInstanceValid(_label) && _label.Visible;

    private OmniLight3D _glow = null!;
    private MeshInstance3D _core = null!;
    private Label3D _label = null!;
    private bool _claimed;

    public void Configure(LootItem item, string englishName, string chineseName)
    {
        Loot.Clear();
        Loot.Add(item);
        Grade = item.Grade;
        EnglishName = englishName;
        ChineseName = chineseName;
        if (IsInsideTree())
        {
            ApplyVisuals();
        }
    }

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 0;
        AddToGroup("graded_loot");
        AddChild(new CollisionShape3D
        {
            Position = new Vector3(0.0f, 0.18f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(0.55f, 0.36f, 0.4f) }
        });
        BuildMesh();
        ApplyVisuals();
    }

    public string DisplayName(string language) => GameLocalization.IsChinese(language) ? ChineseName : EnglishName;

    public void OnSearched()
    {
        if (_claimed)
        {
            return;
        }
        _claimed = true;
    }

    public void MarkEmpty()
    {
        Loot.Clear();
        if (IsInstanceValid(_glow))
        {
            _glow.Visible = false;
        }
        if (IsInstanceValid(_label))
        {
            _label.Visible = false;
        }
    }

    private void BuildMesh()
    {
        _core = new MeshInstance3D
        {
            Position = new Vector3(0.0f, 0.18f, 0.0f),
            Mesh = new BoxMesh { Size = new Vector3(0.48f, 0.28f, 0.34f) }
        };
        AddChild(_core);
        _glow = new OmniLight3D
        {
            Position = new Vector3(0.0f, 0.42f, 0.0f),
            OmniRange = 3.8f,
            LightEnergy = 1.4f,
            ShadowEnabled = false
        };
        AddChild(_glow);
        _label = new Label3D
        {
            Position = new Vector3(0.0f, 0.62f, 0.0f),
            FontSize = 14,
            OutlineSize = 5,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 22.0f
        };
        AddChild(_label);
    }

    private void ApplyVisuals()
    {
        var color = LootGrades.GlowColor(Grade);
        if (IsInstanceValid(_core))
        {
            _core.Mesh = BuildItemMesh();
            _core.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color * 0.35f,
                EmissionEnabled = true,
                Emission = color,
                EmissionEnergyMultiplier = 1.8f + (int)Grade * 0.35f,
                Metallic = 0.35f,
                Roughness = 0.4f
            };
        }
        if (IsInstanceValid(_glow))
        {
            _glow.LightColor = color;
            _glow.LightEnergy = 1.1f + (int)Grade * 0.35f;
        }
        if (IsInstanceValid(_label))
        {
            _label.Modulate = color;
            _label.Text = Loot.Count > 0
                ? LootGrades.DisplayName(Grade, "zh") + "  " + Loot[0].StackValue
                : string.Empty;
        }
    }

    private PrimitiveMesh BuildItemMesh()
    {
        if (Loot.Count == 0)
        {
            return new BoxMesh { Size = new Vector3(0.48f, 0.28f, 0.34f) };
        }
        var item = Loot[0];
        if (item.Kind == LootItemKind.Medical)
        {
            return item.MedicalKind == MedicalItemKind.Adrenaline
                ? new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = 0.42f, RadialSegments = 12 }
                : new BoxMesh
                {
                    Size = item.MedicalKind == MedicalItemKind.FieldMedkit
                        ? new Vector3(0.5f, 0.3f, 0.38f)
                        : new Vector3(0.38f, 0.16f, 0.28f)
                };
        }
        if (item.Kind == LootItemKind.ArmorPlate)
        {
            return new BoxMesh { Size = new Vector3(0.38f, 0.46f, 0.08f) };
        }
        if (item.Kind == LootItemKind.Valuable)
        {
            return item.ValuableKind switch
            {
                ValuableItemKind.CannedCoffee or ValuableItemKind.DesignerPerfume or ValuableItemKind.AntiqueClock
                    => new CylinderMesh { TopRadius = 0.13f, BottomRadius = 0.13f, Height = 0.34f, RadialSegments = 16 },
                ValuableItemKind.CollectorCoin or ValuableItemKind.Wristwatch
                    => new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.16f, Height = 0.06f, RadialSegments = 20 },
                ValuableItemKind.SmartPhone or ValuableItemKind.EncryptedDrive
                    => new BoxMesh { Size = new Vector3(0.25f, 0.05f, 0.42f) },
                ValuableItemKind.VintageCamera
                    => new BoxMesh { Size = new Vector3(0.42f, 0.3f, 0.24f) },
                _ => new BoxMesh { Size = new Vector3(0.42f, 0.22f, 0.32f) }
            };
        }
        return new BoxMesh { Size = new Vector3(0.48f, 0.28f, 0.34f) };
    }
}
