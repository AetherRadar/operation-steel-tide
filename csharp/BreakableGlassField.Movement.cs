using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class BreakableGlassField
{
    /// <summary>
    /// Dedicated character-movement layer for intact glass. It deliberately does not
    /// overlap world geometry (layer 1) or the query-only glass hit layer (layer 128).
    /// </summary>
    public const uint MovementCollisionLayer = 1u << 6;
    public const uint SightCollisionMask = uint.MaxValue & ~MovementCollisionLayer;

    private readonly Dictionary<uint, int> _paneByMovementShapeOwner = new();
    private StaticBody3D? _movementBody;
    private bool _buildFrames = true;
    private bool _blocksMovementUntilShattered;
    private bool _fieldActive = true;

    public bool BuildsFrames => _buildFrames;
    public bool BlocksMovementUntilShattered => _blocksMovementUntilShattered;
    public bool IsFieldActive => _fieldActive;
    public int MovementBlockerCount => _paneByMovementShapeOwner.Count;

    private void BuildMovementShapes()
    {
        if (!_blocksMovementUntilShattered)
        {
            return;
        }

        _movementBody = new StaticBody3D
        {
            Name = "IntactGlassMovementBlockers",
            CollisionLayer = MovementCollisionLayer,
            CollisionMask = 0,
            InputRayPickable = false
        };
        AddChild(_movementBody);

        for (var index = 0; index < _panes.Count; index++)
        {
            var pane = _panes[index];
            pane.MovementShapeOwner = _movementBody.CreateShapeOwner(this);
            pane.HasMovementShape = true;
            _movementBody.ShapeOwnerSetTransform(
                pane.MovementShapeOwner,
                new Transform3D(Basis.FromEuler(pane.Rotation), pane.Position));
            _movementBody.ShapeOwnerAddShape(
                pane.MovementShapeOwner,
                new BoxShape3D { Size = pane.Size });
            _paneByMovementShapeOwner[pane.MovementShapeOwner] = index;
        }
    }

    private void ApplyFieldCollisionState()
    {
        if (!_committed)
        {
            return;
        }

        CollisionLayer = _fieldActive ? GlassCollisionLayer : 0;
        if (_movementBody is not null)
        {
            _movementBody.CollisionLayer = _fieldActive ? MovementCollisionLayer : 0;
        }

        foreach (var pane in _panes)
        {
            var disabled = !_fieldActive || pane.Shattered;
            ShapeOwnerSetDisabled(pane.ShapeOwner, disabled);
            SetPaneMovementCollisionDisabled(pane, disabled);
        }
    }

    private void SetPaneMovementCollisionDisabled(PaneState pane, bool disabled)
    {
        if (pane.HasMovementShape && _movementBody is not null)
        {
            _movementBody.ShapeOwnerSetDisabled(pane.MovementShapeOwner, disabled);
        }
    }

    /// <summary>
    /// Enables or disables the complete field, including visuals, hit queries, and
    /// optional movement blockers. A re-enabled field preserves which panes shattered.
    /// </summary>
    public void SetFieldActive(bool active)
    {
        _fieldActive = active;
        Visible = active;
        ApplyFieldCollisionState();
    }

    /// <summary>
    /// Restores every pane to its intact visual and collision state. If the field is
    /// inactive, restored collision remains disabled until SetFieldActive(true).
    /// </summary>
    public void ResetAllPanes()
    {
        ApplyShatteredPaneMask(0u);
    }

    public bool HasPaneMovementBlocker(int paneIndex)
    {
        return paneIndex >= 0
            && paneIndex < _panes.Count
            && _panes[paneIndex].HasMovementShape;
    }

    public bool IsPaneMovementCollisionDisabled(int paneIndex)
    {
        if (!HasPaneMovementBlocker(paneIndex) || _movementBody is null)
        {
            return true;
        }
        return _movementBody.IsShapeOwnerDisabled(_panes[paneIndex].MovementShapeOwner);
    }

    public bool TryShatterPane(
        int paneIndex,
        Vector3 hitPosition,
        Vector3 hitNormal,
        Vector3 shotDirection,
        bool spawnEffects = true)
    {
        return _fieldActive
            && _hasLocalShatterAuthority
            && ShatterPane(paneIndex, hitPosition, hitNormal, shotDirection, spawnEffects);
    }

    /// <summary>
    /// Lets AI characters clear an intact movement-blocking pane after MoveAndSlide
    /// reports contact. Player movement intentionally does not call this helper.
    /// </summary>
    public static bool TryShatterMovementBlockerFromCollisions(
        CharacterBody3D actor,
        bool spawnEffects = true)
    {
        for (var collisionIndex = 0; collisionIndex < actor.GetSlideCollisionCount(); collisionIndex++)
        {
            var collision = actor.GetSlideCollision(collisionIndex);
            if (collision.GetCollider() is not StaticBody3D movementBody
                || movementBody.GetParent() is not BreakableGlassField field
                || !field._fieldActive
                || !field._blocksMovementUntilShattered)
            {
                continue;
            }

            var shapeIndex = collision.GetColliderShapeIndex();
            var shapeOwner = movementBody.ShapeFindOwner(shapeIndex);
            if (!field._paneByMovementShapeOwner.TryGetValue(shapeOwner, out var paneIndex))
            {
                continue;
            }

            var shotDirection = actor.Velocity.LengthSquared() > 0.001f
                ? actor.Velocity.Normalized()
                : -collision.GetNormal();
            if (!field._hasLocalShatterAuthority)
            {
                return false;
            }
            return field.ShatterPane(
                paneIndex,
                collision.GetPosition(),
                collision.GetNormal(),
                shotDirection,
                spawnEffects);
        }
        return false;
    }
}
