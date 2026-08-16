using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>Sealed field cache by default; player-dropped items use the loose pickup presentation.</summary>
[GlobalClass]
public partial class GradedLootPickup : StaticBody3D, ILootSource, IOpenableLootSource
{
    public List<LootItem> Loot { get; } = new();
    public Node3D LootNode => this;
    public bool IsSearchable => Loot.Count > 0;
    public float SearchDuration => 0.55f;
    public LootGrade Grade { get; private set; } = LootGrade.Common;
    public string EnglishName { get; set; } = "Field cache";
    public string ChineseName { get; set; } = "战地物资";

    public bool IsOpened => _loosePresentation || _opened;
    public bool VisualReady => Loot.Count > 0
        && Visible
        && (_loosePresentation
        ? IsInstanceValid(_core) && _core.Visible
            && IsInstanceValid(_glow) && _glow.Visible
            && IsInstanceValid(_label) && _label.Visible
        : IsInstanceValid(_containerRoot)
            && _containerRoot.Visible
            && (IsInstanceValid(_importedContainerVisual) || _containerPartCount >= 3));
    public bool GradeConcealedBeforeOpen => Loot.Count > 0
        && !_loosePresentation
        && !_opened
        && !IsInstanceValid(_glow)
        && !IsInstanceValid(_label);
    public bool OpenVisualReady => _loosePresentation
        || _opened && ((IsInstanceValid(_importedContainerVisual)
            && _openAnimationMeshes.Length > 0
            && ReferenceEquals(_importedContainerVisual.Mesh, _openAnimationMeshes[^1]))
            || (IsInstanceValid(_lid) && Mathf.Abs(_lid.Rotation.X + 1.32f) <= 0.03f));
    internal bool EmptyPresentationHiddenForDiagnostics => Loot.Count == 0
        && !Visible
        && CollisionLayer == 0;

    private OmniLight3D _glow = null!;
    private MeshInstance3D _core = null!;
    private Label3D _label = null!;
    private Node3D _containerRoot = null!;
    private Node3D _lid = null!;
    private MeshInstance3D _importedContainerVisual = null!;
    private ArrayMesh[] _openAnimationMeshes = System.Array.Empty<ArrayMesh>();
    private bool _loosePresentation;
    private bool _opened;
    private int _containerPartCount;

    public void Configure(LootItem item, string englishName, string chineseName)
    {
        Loot.Clear();
        Loot.Add(item);
        Grade = item.Grade;
        EnglishName = englishName;
        ChineseName = chineseName;
        _loosePresentation = false;
        _opened = false;
        if (IsInsideTree())
        {
            ApplyVisuals();
        }
    }

    public void ConfigureDropped(LootItem item, string englishName, string chineseName)
    {
        Configure(item, englishName, chineseName);
        _loosePresentation = true;
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
            Position = new Vector3(0.0f, 0.25f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(0.82f, 0.5f, 0.58f) }
        });
        BuildMesh();
        ApplyVisuals();
    }

    public string DisplayName(string language) => GameLocalization.IsChinese(language) ? ChineseName : EnglishName;

    public void OnSearched()
    {
        if (_opened)
        {
            return;
        }
        _opened = true;
        if (IsInstanceValid(_importedContainerVisual) && _openAnimationMeshes.Length > 1)
        {
            var opening = CreateTween()
                .SetProcessMode(Tween.TweenProcessMode.Idle)
                .SetPauseMode(Tween.TweenPauseMode.Process);
            for (var frame = 1; frame < _openAnimationMeshes.Length; frame++)
            {
                var frameMesh = _openAnimationMeshes[frame];
                opening.TweenInterval(0.055f);
                opening.TweenCallback(Callable.From(() =>
                {
                    if (IsInstanceValid(_importedContainerVisual))
                    {
                        _importedContainerVisual.Mesh = frameMesh;
                    }
                }));
            }
            return;
        }
        if (!_loosePresentation && IsInstanceValid(_lid))
        {
            CreateTween()
                .SetProcessMode(Tween.TweenProcessMode.Idle)
                .SetPauseMode(Tween.TweenPauseMode.Process)
                .TweenProperty(_lid, "rotation:x", -1.32f, 0.38f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
        }
    }

    public void MarkEmpty()
    {
        Loot.Clear();
        RefreshContentsPresentation();
    }

    internal void RefreshContentsPresentation()
    {
        if (Loot.Count > 0)
        {
            Grade = Loot[0].Grade;
        }
        ApplyVisuals();
    }

    private void BuildMesh()
    {
        if (!_loosePresentation)
        {
            BuildContainerMesh();
            return;
        }

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

    private void BuildContainerMesh()
    {
        _containerRoot = this;
        if (ResidentialSupplyCache.TryGetSharedChestAnimation(out var animationMeshes, out _))
        {
            _openAnimationMeshes = animationMeshes;
            _importedContainerVisual = new MeshInstance3D
            {
                Name = "SealedMilitaryFieldCache",
                Mesh = animationMeshes[0],
                Position = new Vector3(0.35f, 0.015f, 0.0f),
                Scale = Vector3.One * 0.7f,
                VisibilityRangeEnd = 46.0f,
                VisibilityRangeEndMargin = 5.0f
            };
            AddChild(_importedContainerVisual);
            return;
        }

        var shell = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.17f, 0.205f, 0.16f),
            Metallic = 0.24f,
            Roughness = 0.78f
        };
        var trim = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.045f, 0.052f, 0.048f),
            Metallic = 0.78f,
            Roughness = 0.34f
        };
        ContainerPart(_containerRoot, new Vector3(0.78f, 0.4f, 0.54f), new Vector3(0.0f, 0.22f, 0.0f), shell);

        _lid = new Node3D
        {
            Name = "SealedFieldCacheLid",
            Position = new Vector3(0.0f, 0.46f, 0.27f)
        };
        AddChild(_lid);
        ContainerPart(_lid, new Vector3(0.82f, 0.12f, 0.58f), new Vector3(0.0f, 0.0f, -0.27f), shell);
        ContainerPart(_lid, new Vector3(0.26f, 0.08f, 0.06f), new Vector3(0.0f, -0.025f, -0.58f), trim);
    }

    private void ContainerPart(Node parent, Vector3 size, Vector3 position, Godot.Material material)
    {
        parent.AddChild(new MeshInstance3D
        {
            Name = $"SealedFieldCachePart_{_containerPartCount++:00}",
            Position = position,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = material
        });
    }

    private void ApplyVisuals()
    {
        var hasContents = Loot.Count > 0;
        Visible = hasContents;
        CollisionLayer = hasContents ? 1u : 0u;
        var color = LootGrades.GlowColor(Grade);
        if (IsInstanceValid(_core))
        {
            _core.Visible = _loosePresentation && hasContents;
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
            _glow.Visible = _loosePresentation && hasContents;
            _glow.LightColor = color;
            _glow.LightEnergy = 1.1f + (int)Grade * 0.35f;
        }
        if (IsInstanceValid(_label))
        {
            _label.Visible = _loosePresentation && hasContents;
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
