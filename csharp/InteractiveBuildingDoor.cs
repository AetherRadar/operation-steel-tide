using Godot;

namespace OperationSteelTide;

/// <summary>An authored overhead door whose visual and collision move together.</summary>
public partial class InteractiveBuildingDoor : AnimatableBody3D
{
    private const string DoorScenePath = "res://assets/models/kenney_factory_kit/door-wide-closed.glb";
    private const float SourceWidth = 1.8f;
    private const float SourceHeight = 1.6f;
    private const float SourceDepthCenter = 0.4f;
    private const float OpenRotationDegrees = 88.0f;
    private const float MotionDuration = 0.58f;

    private float _width;
    private float _height;
    private float _frontZ;
    private float _visibilityRange;
    private Vector3 _interactionLocal;
    private CollisionShape3D _doorCollision = null!;
    private Node3D _authoredVisual = null!;
    private Tween? _motionTween;

    public int DoorId { get; private set; }
    public bool IsOpen { get; private set; }
    public bool IsAnimating { get; private set; }
    public bool TargetOpen { get; private set; }
    public int CompletedMotionCount { get; private set; }
    public bool UsesAuthoredVisual => IsInstanceValid(_authoredVisual);
    public bool HasBoxCollision => IsInstanceValid(_doorCollision)
        && _doorCollision.Shape is BoxShape3D;
    public float MotionAngleDegrees => RotationDegrees.X;

    public Vector3 InteractionPoint
        => GetParent() is Node3D parent ? parent.ToGlobal(_interactionLocal) : GlobalPosition;

    public Vector3 OutsideProbe
        => ParentPoint(new Vector3(0, Mathf.Min(1.4f, _height * 0.5f), _frontZ + 0.72f));

    public Vector3 InsideProbe
        => ParentPoint(new Vector3(0, Mathf.Min(1.4f, _height * 0.5f), _frontZ - 0.72f));

    public void Configure(
        int doorId,
        float doorwayWidth,
        float doorwayHeight,
        float frontZ,
        float visibilityRange)
    {
        DoorId = doorId;
        _width = Mathf.Max(1.0f, doorwayWidth * 0.96f);
        _height = Mathf.Max(1.8f, doorwayHeight * 0.98f);
        _frontZ = frontZ;
        _visibilityRange = Mathf.Max(80.0f, visibilityRange);
        _interactionLocal = new Vector3(0, Mathf.Min(1.35f, _height * 0.5f), frontZ);
        Position = new Vector3(0, _height, frontZ);
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
            Position = new Vector3(0, -_height * 0.5f, 0),
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
        _motionTween.TweenProperty(
            this,
            "rotation:x",
            Mathf.DegToRad(open ? OpenRotationDegrees : 0.0f),
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
        Rotation = new Vector3(Mathf.DegToRad(open ? OpenRotationDegrees : 0.0f), 0, 0);
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
            Transform = new Transform3D(parent.GlobalBasis, InteractionPoint),
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
        var scene = GD.Load<PackedScene>(DoorScenePath);
        if (scene?.Instantiate() is not Node3D visual)
        {
            return;
        }
        _authoredVisual = visual;
        visual.Name = "AuthoredOverheadDoor";
        visual.Position = new Vector3(0, -_height, -SourceDepthCenter);
        visual.Scale = new Vector3(_width / SourceWidth, _height / SourceHeight, 1.0f);
        visual.AddToGroup("refinery_door_authored");
        ConfigureVisuals(visual);
        AddChild(visual);
    }

    private void ConfigureVisuals(Node node)
    {
        if (node is GeometryInstance3D visual)
        {
            visual.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            visual.VisibilityRangeEnd = _visibilityRange;
            visual.VisibilityRangeEndMargin = Mathf.Min(18.0f, _visibilityRange * 0.12f);
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
        => GetParent() is Node3D parent ? parent.ToGlobal(localPoint) : GlobalPosition;
}
