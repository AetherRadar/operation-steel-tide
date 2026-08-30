using Godot;

namespace OperationSteelTide;

public partial class InteractiveBuildingDoor
{
    private float _baseVisibilityRange;
    private float _effectiveVisibilityRange;

    public float AuthoredBaseVisibilityRange => _baseVisibilityRange;
    public float AuthoredEffectiveVisibilityRange => _effectiveVisibilityRange;
    public int AuthoredRenderableGeometryCount
        => _motionStyle == BuildingDoorMotionStyle.DoubleHinged
            ? CountAuthoredRenderableGeometry(_leftLeafVisual)
                + CountAuthoredRenderableGeometry(_rightLeafVisual)
            : CountAuthoredRenderableGeometry(_authoredVisual);
    public bool HasRenderableAuthoredVisualGeometry
        => _motionStyle == BuildingDoorMotionStyle.DoubleHinged
            ? CountAuthoredRenderableGeometry(_leftLeafVisual) > 0
                && CountAuthoredRenderableGeometry(_rightLeafVisual) > 0
            : CountAuthoredRenderableGeometry(_authoredVisual) > 0;

    // Compatibility projection for existing diagnostics and callers.
    public float AuthoredVisibilityRange => AuthoredEffectiveVisibilityRange;

    public bool HasAppliedAuthoredVisibilityRange
        => CountAuthoredVisualVisibilityMismatches(this) == 0;

    public void ApplyVisibilityScale(float distanceScale)
    {
        _effectiveVisibilityRange = _baseVisibilityRange
            * Mathf.Clamp(distanceScale, 0.1f, 1.0f);
        if (IsInsideTree())
        {
            ApplyAuthoredVisualVisibility(this);
        }
    }

    private void ConfigureVisualVisibility(GeometryInstance3D visual)
    {
        visual.VisibilityRangeEnd = _effectiveVisibilityRange;
        visual.VisibilityRangeEndMargin = Mathf.Min(
            28.0f,
            _effectiveVisibilityRange * 0.12f);
        visual.VisibilityRangeFadeMode =
            GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
    }

    private void ApplyAuthoredVisualVisibility(Node node)
    {
        if (node is GeometryInstance3D visual)
        {
            ConfigureVisualVisibility(visual);
        }
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                ApplyAuthoredVisualVisibility(childNode);
            }
        }
    }

    private int CountAuthoredVisualVisibilityMismatches(Node node)
    {
        var expectedMargin = Mathf.Min(
            28.0f,
            _effectiveVisibilityRange * 0.12f);
        var mismatchCount = node is GeometryInstance3D visual
            && (Mathf.Abs(visual.VisibilityRangeEnd - _effectiveVisibilityRange) > 0.01f
                || Mathf.Abs(visual.VisibilityRangeEndMargin - expectedMargin) > 0.01f
                || visual.VisibilityRangeFadeMode
                    != GeometryInstance3D.VisibilityRangeFadeModeEnum.Self)
                    ? 1
                    : 0;
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                mismatchCount += CountAuthoredVisualVisibilityMismatches(childNode);
            }
        }
        return mismatchCount;
    }

    private static int CountAuthoredRenderableGeometry(Node? node)
    {
        if (!GodotObject.IsInstanceValid(node))
        {
            return 0;
        }

        var count = node is MeshInstance3D
        {
            Mesh: { } mesh,
            Layers: not 0
        } visual
            && mesh.GetSurfaceCount() > 0
            && visual.IsVisibleInTree()
                ? 1
                : 0;
        var children = node!.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                count += CountAuthoredRenderableGeometry(childNode);
            }
        }
        return count;
    }
}
