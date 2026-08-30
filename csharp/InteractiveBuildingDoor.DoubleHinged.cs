using Godot;

namespace OperationSteelTide;

public partial class InteractiveBuildingDoor
{
    private AnimatableBody3D _leftLeaf = null!;
    private AnimatableBody3D _rightLeaf = null!;
    private CollisionShape3D _leftLeafCollision = null!;
    private CollisionShape3D _rightLeafCollision = null!;
    private Node3D _leftLeafVisual = null!;
    private Node3D _rightLeafVisual = null!;

    public int LeafCount => _motionStyle == BuildingDoorMotionStyle.DoubleHinged ? 2 : 1;
    public int LeafCollisionCount => _motionStyle == BuildingDoorMotionStyle.DoubleHinged
        ? (IsBoxCollision(_leftLeafCollision) ? 1 : 0)
            + (IsBoxCollision(_rightLeafCollision) ? 1 : 0)
        : IsBoxCollision(_doorCollision) ? 1 : 0;
    public bool HasBoxCollision => LeafCollisionCount == LeafCount;
    public float LeftLeafAngleDegrees => _motionStyle switch
    {
        BuildingDoorMotionStyle.DoubleHinged when IsInstanceValid(_leftLeaf)
            => Mathf.RadToDeg(Mathf.AngleDifference(0.0f, _leftLeaf.Rotation.Y)),
        BuildingDoorMotionStyle.Hinged
            => Mathf.RadToDeg(Mathf.AngleDifference(_mountYaw, Rotation.Y)),
        _ => RotationDegrees.X
    };
    public float RightLeafAngleDegrees
        => _motionStyle == BuildingDoorMotionStyle.DoubleHinged
            && IsInstanceValid(_rightLeaf)
                ? Mathf.RadToDeg(Mathf.AngleDifference(0.0f, _rightLeaf.Rotation.Y))
                : 0.0f;
    public float MotionAngleDegrees => _motionStyle == BuildingDoorMotionStyle.DoubleHinged
        ? Mathf.Min(Mathf.Abs(LeftLeafAngleDegrees), Mathf.Abs(RightLeafAngleDegrees))
        : Mathf.Abs(LeftLeafAngleDegrees);
    internal string DoubleHingedLayoutDiagnostic
        => _motionStyle != BuildingDoorMotionStyle.DoubleHinged
            ? "not_double"
            : $"leaf_pos={_leftLeaf?.Position}/{_rightLeaf?.Position}"
                + $":visual_pos={_leftLeafVisual?.Position}/{_rightLeafVisual?.Position}"
                + $":left_basis={_leftLeafVisual?.Basis}"
                + $":right_basis={_rightLeafVisual?.Basis}"
                + $":scale={_leftLeafVisual?.Scale}/{_rightLeafVisual?.Scale}";

    internal void AddCollisionExclusions(Godot.Collections.Array<Rid> exclusions)
    {
        if (_motionStyle == BuildingDoorMotionStyle.DoubleHinged)
        {
            if (IsInstanceValid(_leftLeaf))
            {
                exclusions.Add(_leftLeaf.GetRid());
            }
            if (IsInstanceValid(_rightLeaf))
            {
                exclusions.Add(_rightLeaf.GetRid());
            }
            return;
        }
        exclusions.Add(GetRid());
    }

    private bool StartDoubleHingedMotion(bool open)
    {
        if (!IsInstanceValid(_leftLeaf)
            || !IsInstanceValid(_rightLeaf)
            || _motionTween is not { } tween)
        {
            IsAnimating = false;
            TargetOpen = IsOpen;
            _motionTween?.Kill();
            _motionTween = null;
            return false;
        }

        var leafAngle = Mathf.DegToRad(open ? OpenAngleDegrees : 0.0f);
        tween.SetParallel(true);
        tween.TweenProperty(_leftLeaf, "rotation:y", leafAngle, MotionDuration);
        tween.TweenProperty(_rightLeaf, "rotation:y", -leafAngle, MotionDuration);
        tween.Chain().TweenCallback(Callable.From(() => CompleteMotion(open)));
        return true;
    }

    private void SetDoubleHingedOpenImmediate(float angle)
    {
        if (IsInstanceValid(_leftLeaf))
        {
            _leftLeaf.Rotation = new Vector3(0, angle, 0);
        }
        if (IsInstanceValid(_rightLeaf))
        {
            _rightLeaf.Rotation = new Vector3(0, -angle, 0);
        }
    }

    private void BuildDoubleHingedLeaves()
    {
        var scene = GD.Load<PackedScene>(_visualScenePath);
        if (scene is null)
        {
            return;
        }

        var leafWidth = _width * 0.5f;
        _leftLeaf = CreateDoubleLeaf("Left", -_width * 0.5f);
        _rightLeaf = CreateDoubleLeaf("Right", _width * 0.5f);
        AddChild(_leftLeaf);
        AddChild(_rightLeaf);

        _leftLeafCollision = CreateLeafCollision(leafWidth, leafWidth * 0.5f);
        _rightLeafCollision = CreateLeafCollision(leafWidth, -leafWidth * 0.5f);
        _leftLeaf.AddChild(_leftLeafCollision);
        _rightLeaf.AddChild(_rightLeafCollision);

        _leftLeafVisual = InstantiateDoubleLeafVisual(
            scene,
            "AuthoredDoubleHingedDoorLeft",
            mirrorFromRightHinge: false,
            leafWidth);
        _rightLeafVisual = InstantiateDoubleLeafVisual(
            scene,
            "AuthoredDoubleHingedDoorRight",
            mirrorFromRightHinge: true,
            leafWidth);
        if (IsInstanceValid(_leftLeafVisual))
        {
            _leftLeaf.AddChild(_leftLeafVisual);
        }
        if (IsInstanceValid(_rightLeafVisual))
        {
            _rightLeaf.AddChild(_rightLeafVisual);
        }
    }

    private static AnimatableBody3D CreateDoubleLeaf(string side, float hingeX)
        => new()
        {
            Name = $"DoubleHingedDoor{side}Leaf",
            Position = new Vector3(hingeX, 0, 0),
            CollisionLayer = 1,
            CollisionMask = 0,
            SyncToPhysics = true
        };

    private CollisionShape3D CreateLeafCollision(float leafWidth, float centerX)
        => new()
        {
            Name = "InteractiveDoorCollision",
            Position = new Vector3(centerX, _height * 0.5f, 0),
            Shape = new BoxShape3D
            {
                Size = new Vector3(leafWidth + 0.04f, _height, 0.18f)
            }
        };

    private Node3D InstantiateDoubleLeafVisual(
        PackedScene scene,
        string name,
        bool mirrorFromRightHinge,
        float leafWidth)
    {
        if (scene.Instantiate() is not Node3D visual)
        {
            return null!;
        }
        visual.Name = name;
        visual.Position = Vector3.Zero;
        visual.Rotation = mirrorFromRightHinge
            ? new Vector3(0, Mathf.Pi, 0)
            : Vector3.Zero;
        visual.Scale = new Vector3(
            leafWidth / _sourceWidth,
            _height / _sourceHeight,
            0.72f);
        visual.AddToGroup("refinery_door_authored");
        ConfigureVisuals(visual);
        return visual;
    }

    private int GetDoubleHingedAuthoredVisualPanelCount()
        => (IsInstanceValid(_leftLeafVisual) ? 1 : 0)
            + (IsInstanceValid(_rightLeafVisual) ? 1 : 0);

    private float GetDoubleHingedMaxAspectDistortion()
    {
        if (!IsInstanceValid(_leftLeafVisual) || !IsInstanceValid(_rightLeafVisual))
        {
            return float.PositiveInfinity;
        }
        return Mathf.Max(
            AspectDistortion(_leftLeafVisual.Scale.X, _leftLeafVisual.Scale.Y),
            AspectDistortion(_rightLeafVisual.Scale.X, _rightLeafVisual.Scale.Y));
    }

    private bool ValidateDoubleHingedAuthoredVisualLayout()
    {
        var expectedScale = new Vector3(
            _width * 0.5f / _sourceWidth,
            _height / _sourceHeight,
            0.72f);
        return IsInstanceValid(_leftLeaf)
            && IsInstanceValid(_rightLeaf)
            && IsInstanceValid(_leftLeafVisual)
            && IsInstanceValid(_rightLeafVisual)
            && _leftLeaf.GetParent() == this
            && _rightLeaf.GetParent() == this
            && _leftLeafVisual.GetParent() == _leftLeaf
            && _rightLeafVisual.GetParent() == _rightLeaf
            && _leftLeaf.Position.DistanceTo(
                new Vector3(-_width * 0.5f, 0, 0)) <= 0.001f
            && _rightLeaf.Position.DistanceTo(
                new Vector3(_width * 0.5f, 0, 0)) <= 0.001f
            && _leftLeafVisual.Position.Length() <= 0.001f
            && _rightLeafVisual.Position.Length() <= 0.001f
            && HasExpectedDoubleLeafBasis(
                _leftLeafVisual,
                expectedScale,
                rotatedFromRightHinge: false)
            && HasExpectedDoubleLeafBasis(
                _rightLeafVisual,
                expectedScale,
                rotatedFromRightHinge: true);
    }

    private static bool HasExpectedDoubleLeafBasis(
        Node3D visual,
        Vector3 expectedScale,
        bool rotatedFromRightHinge)
    {
        var expectedX = (rotatedFromRightHinge ? Vector3.Left : Vector3.Right)
            * expectedScale.X;
        var expectedZ = (rotatedFromRightHinge ? Vector3.Forward : Vector3.Back)
            * expectedScale.Z;
        return visual.Basis.X.DistanceTo(expectedX) <= 0.001f
            && visual.Basis.Y.DistanceTo(Vector3.Up * expectedScale.Y) <= 0.001f
            && visual.Basis.Z.DistanceTo(expectedZ) <= 0.001f;
    }

    private static bool IsBoxCollision(CollisionShape3D collision)
        => IsInstanceValid(collision) && collision.Shape is BoxShape3D;

    private float OpenAngleDegrees => _motionStyle is BuildingDoorMotionStyle.Hinged
        or BuildingDoorMotionStyle.DoubleHinged
        ? HingedOpenRotationDegrees
        : OverheadOpenRotationDegrees;
}
