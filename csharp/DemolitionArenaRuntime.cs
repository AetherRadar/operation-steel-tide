using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>Owns the generated arena node tree and its collision activation lifecycle.</summary>
public sealed class DemolitionArenaRuntime
{
    private readonly List<StaticBody3D> _staticBodies;
    private readonly IReadOnlyList<BreakableGlassField> _bazaarGlassFields;

    public DemolitionArenaLayout Layout { get; }
    public Node3D Root { get; }
    public IReadOnlyList<Node3D> Sites { get; }
    public IReadOnlyList<StaticBody3D> StaticBodies => _staticBodies;
    public IReadOnlyList<BreakableGlassField> BazaarGlassFields => _bazaarGlassFields;
    public IReadOnlyList<Vector3> CoverPoints => Layout.CoverPoints;
    public bool Active { get; private set; }
    public int CollisionBodyCount => _staticBodies.Count;
    public int ActiveCollisionBodyCount { get; private set; }
    public int VisualPartCount { get; }

    internal DemolitionArenaRuntime(
        DemolitionArenaLayout layout,
        Node3D root,
        IReadOnlyList<Node3D> sites,
        List<StaticBody3D> staticBodies,
        IReadOnlyList<BreakableGlassField> bazaarGlassFields,
        int visualPartCount)
    {
        Layout = layout;
        Root = root;
        Sites = sites;
        _staticBodies = staticBodies;
        _bazaarGlassFields = bazaarGlassFields;
        VisualPartCount = visualPartCount;
        SetActive(false);
    }

    public void SetActive(bool active)
    {
        Active = active;
        Root.Visible = active;
        Root.ProcessMode = active ? Node.ProcessModeEnum.Inherit : Node.ProcessModeEnum.Disabled;
        var collisionLayer = active ? 1u : 0u;
        for (var index = 0; index < _staticBodies.Count; index++)
        {
            _staticBodies[index].CollisionLayer = collisionLayer;
        }
        for (var index = 0; index < _bazaarGlassFields.Count; index++)
        {
            var field = _bazaarGlassFields[index];
            if (GodotObject.IsInstanceValid(field))
            {
                field.SetFieldActive(active);
            }
        }
        ActiveCollisionBodyCount = active ? _staticBodies.Count : 0;
    }

    public void ResetRoundState()
    {
        for (var index = 0; index < _bazaarGlassFields.Count; index++)
        {
            var field = _bazaarGlassFields[index];
            if (GodotObject.IsInstanceValid(field))
            {
                field.ResetAllPanes();
            }
        }
    }

    public bool AllStaticBodiesUseWorldLayer()
    {
        var expectedLayer = Active ? 1u : 0u;
        for (var index = 0; index < _staticBodies.Count; index++)
        {
            var body = _staticBodies[index];
            if (!GodotObject.IsInstanceValid(body)
                || body.CollisionLayer != expectedLayer
                || body.CollisionMask != 0)
            {
                return false;
            }
        }
        return true;
    }

    public bool Owns(Node node) => node == Root || Root.IsAncestorOf(node);

    public Vector3 SitePosition(int index) => Layout.SitePosition(index);
}
