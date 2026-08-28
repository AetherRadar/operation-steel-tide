using Godot;

namespace OperationSteelTide;

public enum BuildingDoorMotionStyle
{
    Overhead,
    Hinged
}

/// <summary>An authored building door whose visual and collision move together.</summary>
public partial class InteractiveBuildingDoor : AnimatableBody3D
{
    public const string OverheadDoorScenePath =
        "res://assets/models/kenney_factory_kit/door-wide-closed.glb";
    public const string HingedDoorScenePath =
        "res://assets/models/kenney_factory_kit/door-hinged.glb";

    private const float OverheadSourceWidth = 1.8f;
    private const float OverheadSourceHeight = 1.6f;
    private const float OverheadSourceDepthCenter = 0.4f;
    private const float HingedSourceWidth = 0.8f;
    private const float HingedSourceHeight = 1.6f;
    private const float OverheadOpenRotationDegrees = 88.0f;
    private const float HingedOpenRotationDegrees = 96.0f;
    private const float MotionDuration = 0.58f;

    private float _width;
    private float _height;
    private float _frontZ;
    private float _visibilityRange;
    private Vector3 _mountPosition;
    private float _mountYaw;
    private string _visualScenePath = OverheadDoorScenePath;
    private float _sourceWidth = OverheadSourceWidth;
    private float _sourceHeight = OverheadSourceHeight;
    private float _sourceDepthCenter = OverheadSourceDepthCenter;
    private int _visualPanelCount = 1;
    private bool _usesCustomVisualScene;
    private Vector3 _interactionLocal;
    private CollisionShape3D _doorCollision = null!;
    private Node3D _authoredVisual = null!;
    private Tween? _motionTween;
    private BuildingDoorMotionStyle _motionStyle;

    public int DoorId { get; private set; }
    public bool IsOpen { get; private set; }
    public bool IsAnimating { get; private set; }
    public bool TargetOpen { get; private set; }
    public int CompletedMotionCount { get; private set; }
    public bool UsesAuthoredVisual => IsInstanceValid(_authoredVisual);
    public int AuthoredVisualPanelCount => GetAuthoredVisualPanelCount();
    public float MaxAuthoredVisualAspectDistortion
        => GetMaxAuthoredVisualAspectDistortion();
    public bool HasValidAuthoredVisualPanelLayout
        => ValidateAuthoredVisualPanelLayout();
    public bool HasBoxCollision => IsInstanceValid(_doorCollision)
        && _doorCollision.Shape is BoxShape3D;
    public float MotionAngleDegrees => _motionStyle == BuildingDoorMotionStyle.Hinged
        ? Mathf.Abs(Mathf.RadToDeg(Mathf.AngleDifference(_mountYaw, Rotation.Y)))
        : Mathf.Abs(RotationDegrees.X);
    public BuildingDoorMotionStyle MotionStyle => _motionStyle;
    internal float WidthForNavigation => _width;

    public Vector3 InteractionPoint => ParentPoint(_interactionLocal);

    public Vector3 OutsideProbe
        => ParentPoint(new Vector3(0, Mathf.Min(1.4f, _height * 0.5f), _frontZ + 0.72f));

    public Vector3 InsideProbe
        => ParentPoint(new Vector3(0, Mathf.Min(1.4f, _height * 0.5f), _frontZ - 0.72f));

    public void Configure(
        int doorId,
        float doorwayWidth,
        float doorwayHeight,
        float frontZ,
        float visibilityRange,
        BuildingDoorMotionStyle motionStyle = BuildingDoorMotionStyle.Overhead,
        Vector3 mountPosition = default,
        float mountYaw = 0.0f,
        string? visualScenePath = null,
        float? sourceWidth = null,
        float? sourceHeight = null,
        float? sourceDepthCenter = null,
        int visualPanelCount = 1)
    {
        DoorId = doorId;
        _width = Mathf.Max(1.0f, doorwayWidth * 0.96f);
        _height = Mathf.Max(1.8f, doorwayHeight * 0.98f);
        _frontZ = frontZ;
        _visibilityRange = Mathf.Max(80.0f, visibilityRange);
        _motionStyle = motionStyle;
        _mountPosition = mountPosition;
        _mountYaw = mountYaw;
        _usesCustomVisualScene = !string.IsNullOrWhiteSpace(visualScenePath);
        _visualScenePath = string.IsNullOrWhiteSpace(visualScenePath)
            ? motionStyle == BuildingDoorMotionStyle.Hinged
                ? HingedDoorScenePath
                : OverheadDoorScenePath
            : visualScenePath!;
        var defaultSourceWidth = motionStyle == BuildingDoorMotionStyle.Hinged
            ? HingedSourceWidth
            : OverheadSourceWidth;
        var defaultSourceHeight = motionStyle == BuildingDoorMotionStyle.Hinged
            ? HingedSourceHeight
            : OverheadSourceHeight;
        var defaultSourceDepthCenter = motionStyle == BuildingDoorMotionStyle.Hinged
            ? 0.0f
            : OverheadSourceDepthCenter;
        _sourceWidth = Mathf.Max(0.01f, sourceWidth ?? defaultSourceWidth);
        _sourceHeight = Mathf.Max(0.01f, sourceHeight ?? defaultSourceHeight);
        _sourceDepthCenter = sourceDepthCenter ?? defaultSourceDepthCenter;
        _visualPanelCount = Mathf.Max(1, visualPanelCount);
        _interactionLocal = new Vector3(0, Mathf.Min(1.35f, _height * 0.5f), frontZ);
        var pivot = motionStyle == BuildingDoorMotionStyle.Hinged
            ? new Vector3(-_width * 0.5f, 0.0f, frontZ)
            : new Vector3(0, _height, frontZ);
        Position = MountPoint(pivot);
        Rotation = new Vector3(0, _mountYaw, 0);
    }

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 0;
        SyncToPhysics = true;
        AddToGroup("refinery_interactive_door");
        BuildAuthoredVisual();
        _doorCollision = new CollisionShape3D
        {
            Name = "InteractiveDoorCollision",
            Position = _motionStyle == BuildingDoorMotionStyle.Hinged
                ? new Vector3(_width * 0.5f, _height * 0.5f, 0)
                : new Vector3(0, -_height * 0.5f, 0),
            Shape = new BoxShape3D { Size = new Vector3(_width, _height, 0.18f) }
        };
        AddChild(_doorCollision);
    }

    public string InteractionLabel(string language)
    {
        if (IsAnimating)
        {
            return GameLocalization.Get("door_moving", language, "DOOR MOVING");
        }
        return IsOpen
            ? GameLocalization.Get("close_door", language, "CLOSE DOOR")
            : GameLocalization.Get("open_door", language, "OPEN DOOR");
    }

    public bool TryToggle(bool bypassClearance = false)
        => TrySetOpen(!TargetOpen, bypassClearance);

    public bool TrySetOpen(bool open, bool bypassClearance = false)
    {
        if (IsAnimating || open == TargetOpen)
        {
            return false;
        }
        if (!open && !bypassClearance && !CanCloseWithoutObstruction())
        {
            return false;
        }

        TargetOpen = open;
        IsAnimating = true;
        _motionTween?.Kill();
        _motionTween = CreateTween()
            .SetTrans(Tween.TransitionType.Quart)
            .SetEase(open ? Tween.EaseType.Out : Tween.EaseType.InOut);
        var property = _motionStyle == BuildingDoorMotionStyle.Hinged
            ? "rotation:y"
            : "rotation:x";
        var targetAngle = _motionStyle == BuildingDoorMotionStyle.Hinged
            ? _mountYaw + Mathf.DegToRad(open ? OpenAngleDegrees : 0.0f)
            : Mathf.DegToRad(open ? OpenAngleDegrees : 0.0f);
        _motionTween.TweenProperty(
            this,
            property,
            targetAngle,
            MotionDuration);
        _motionTween.TweenCallback(Callable.From(() => CompleteMotion(open)));
        return true;
    }

    public void SetOpenImmediate(bool open)
    {
        _motionTween?.Kill();
        _motionTween = null;
        TargetOpen = open;
        IsOpen = open;
        IsAnimating = false;
        var angle = Mathf.DegToRad(open ? OpenAngleDegrees : 0.0f);
        Rotation = _motionStyle == BuildingDoorMotionStyle.Hinged
            ? new Vector3(0, _mountYaw + angle, 0)
            : new Vector3(angle, _mountYaw, 0);
    }

    private void CompleteMotion(bool open)
    {
        IsOpen = open;
        TargetOpen = open;
        IsAnimating = false;
        CompletedMotionCount++;
        _motionTween = null;
    }

    private bool CanCloseWithoutObstruction()
    {
        if (!IsInsideTree() || GetParent() is not Node3D parent)
        {
            return false;
        }
        var excludes = new Godot.Collections.Array<Rid> { GetRid() };
        if (parent is CollisionObject3D parentCollider)
        {
            excludes.Add(parentCollider.GetRid());
        }
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = new BoxShape3D
            {
                Size = new Vector3(_width * 0.78f, _height * 0.82f, 1.15f)
            },
            Transform = new Transform3D(
                parent.GlobalBasis * new Basis(Vector3.Up, _mountYaw),
                InteractionPoint),
            CollisionMask = 1 | 2 | 4,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Exclude = excludes
        };
        var hits = GetWorld3D().DirectSpaceState.IntersectShape(query, 12);
        foreach (var hit in hits)
        {
            if (hit.TryGetValue("collider", out var collider)
                && collider.AsGodotObject() is CharacterBody3D or RigidBody3D)
            {
                return false;
            }
        }
        return true;
    }

    private void BuildAuthoredVisual()
    {
        var scene = GD.Load<PackedScene>(_visualScenePath);
        if (scene is null)
        {
            return;
        }
        if (_motionStyle == BuildingDoorMotionStyle.Hinged)
        {
            if (scene.Instantiate() is not Node3D visual)
            {
                return;
            }
            _authoredVisual = visual;
            visual.Name = "AuthoredHingedDoor";
            visual.Position = new Vector3(_width * 0.5f, 0, 0);
            var horizontalScale = _width / _sourceWidth;
            var verticalScale = _height / _sourceHeight;
            visual.Scale = new Vector3(horizontalScale, verticalScale, 0.72f);
            visual.AddToGroup("refinery_door_authored");
            ConfigureVisuals(visual);
            AddChild(visual);
            return;
        }

        var panelRoot = new Node3D
        {
            Name = "AuthoredOverheadDoorPanels",
            Position = new Vector3(0, -_height, -_sourceDepthCenter)
        };
        var panelWidth = _width / _visualPanelCount;
        var panelHorizontalScale = panelWidth / _sourceWidth;
        var panelVerticalScale = _height / _sourceHeight;
        var firstPanelCenter = -_width * 0.5f + panelWidth * 0.5f;
        for (var index = 0; index < _visualPanelCount; index++)
        {
            if (scene.Instantiate() is not Node3D panel)
            {
                panelRoot.Free();
                return;
            }
            panel.Name = $"AuthoredOverheadDoorPanel{index + 1:00}";
            panel.Position = new Vector3(firstPanelCenter + panelWidth * index, 0, 0);
            panel.Scale = new Vector3(
                panelHorizontalScale,
                panelVerticalScale,
                1.0f);
            panelRoot.AddChild(panel);
        }
        _authoredVisual = panelRoot;
        panelRoot.AddToGroup("refinery_door_authored");
        ConfigureVisuals(panelRoot);
        AddChild(panelRoot);
    }

    private static float AspectDistortion(float horizontalScale, float verticalScale)
        => Mathf.Max(horizontalScale, verticalScale)
            / Mathf.Max(0.0001f, Mathf.Min(horizontalScale, verticalScale));

    private int GetAuthoredVisualPanelCount()
    {
        if (!IsInstanceValid(_authoredVisual))
        {
            return 0;
        }
        return _motionStyle == BuildingDoorMotionStyle.Hinged
            ? 1
            : _authoredVisual.GetChildCount();
    }

    private float GetMaxAuthoredVisualAspectDistortion()
    {
        if (!IsInstanceValid(_authoredVisual))
        {
            return float.PositiveInfinity;
        }
        if (_motionStyle == BuildingDoorMotionStyle.Hinged)
        {
            return AspectDistortion(_authoredVisual.Scale.X, _authoredVisual.Scale.Y);
        }

        var maximum = 0.0f;
        for (var index = 0; index < _authoredVisual.GetChildCount(); index++)
        {
            if (_authoredVisual.GetChild(index) is not Node3D panel)
            {
                return float.PositiveInfinity;
            }
            maximum = Mathf.Max(maximum, AspectDistortion(panel.Scale.X, panel.Scale.Y));
        }
        return maximum > 0.0f ? maximum : float.PositiveInfinity;
    }

    private bool ValidateAuthoredVisualPanelLayout()
    {
        if (!IsInstanceValid(_authoredVisual) || _authoredVisual.GetParent() != this)
        {
            return false;
        }
        if (_motionStyle == BuildingDoorMotionStyle.Hinged)
        {
            return _authoredVisual.Name == "AuthoredHingedDoor";
        }
        if (_authoredVisual.Name != "AuthoredOverheadDoorPanels"
            || _authoredVisual.GetChildCount() != _visualPanelCount
            || !_authoredVisual.Position.IsEqualApprox(
                new Vector3(0, -_height, -_sourceDepthCenter)))
        {
            return false;
        }

        var panelWidth = _width / _visualPanelCount;
        var panelHorizontalScale = panelWidth / _sourceWidth;
        var panelVerticalScale = _height / _sourceHeight;
        var firstPanelCenter = -_width * 0.5f + panelWidth * 0.5f;
        for (var index = 0; index < _visualPanelCount; index++)
        {
            if (_authoredVisual.GetChild(index) is not Node3D panel
                || panel.GetParent() != _authoredVisual
                || panel.Name != $"AuthoredOverheadDoorPanel{index + 1:00}"
                || !panel.Position.IsEqualApprox(
                    new Vector3(firstPanelCenter + panelWidth * index, 0, 0))
                || !panel.Scale.IsEqualApprox(
                    new Vector3(panelHorizontalScale, panelVerticalScale, 1.0f)))
            {
                return false;
            }
        }
        return true;
    }

    private void ConfigureVisuals(Node node)
    {
        if (node is GeometryInstance3D visual)
        {
            visual.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            visual.VisibilityRangeEnd = _visibilityRange;
            visual.VisibilityRangeEndMargin = Mathf.Min(18.0f, _visibilityRange * 0.12f);
        }
        if (!_usesCustomVisualScene
            && node is MeshInstance3D { Mesh: not null } meshInstance)
        {
            for (var surface = 0; surface < meshInstance.Mesh.GetSurfaceCount(); surface++)
            {
                if (meshInstance.Mesh.SurfaceGetMaterial(surface) is not BaseMaterial3D source
                    || source.Duplicate(true) is not BaseMaterial3D finish)
                {
                    continue;
                }
                var tint = _motionStyle == BuildingDoorMotionStyle.Hinged
                    ? new Color(0.42f, 0.40f, 0.31f)
                    : new Color(0.36f, 0.38f, 0.31f);
                finish.AlbedoColor = new Color(
                    source.AlbedoColor.R * tint.R,
                    source.AlbedoColor.G * tint.G,
                    source.AlbedoColor.B * tint.B,
                    source.AlbedoColor.A);
                finish.Metallic = Mathf.Max(source.Metallic, 0.34f);
                finish.Roughness = Mathf.Max(source.Roughness, 0.64f);
                meshInstance.SetSurfaceOverrideMaterial(surface, finish);
            }
        }
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                ConfigureVisuals(childNode);
            }
        }
    }

    private Vector3 ParentPoint(Vector3 localPoint)
        => GetParent() is Node3D parent
            ? parent.ToGlobal(MountPoint(localPoint))
            : GlobalPosition;

    private Vector3 MountPoint(Vector3 localPoint)
        => _mountPosition + new Basis(Vector3.Up, _mountYaw) * localPoint;

    private float OpenAngleDegrees => _motionStyle == BuildingDoorMotionStyle.Hinged
        ? HingedOpenRotationDegrees
        : OverheadOpenRotationDegrees;
}
