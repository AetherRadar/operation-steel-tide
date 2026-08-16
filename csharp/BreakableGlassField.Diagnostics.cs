using Godot;

namespace OperationSteelTide;

public partial class BreakableGlassField
{
    internal sealed class DiagnosticsSnapshot
    {
        internal readonly int ShatteredCount;
        internal readonly Vector3 LastShatterPosition;
        internal readonly bool Visible;
        internal readonly PaneDiagnosticsState[] Panes;

        internal DiagnosticsSnapshot(
            int shatteredCount,
            Vector3 lastShatterPosition,
            bool visible,
            PaneDiagnosticsState[] panes)
        {
            ShatteredCount = shatteredCount;
            LastShatterPosition = lastShatterPosition;
            Visible = visible;
            Panes = panes;
        }
    }

    internal readonly struct PaneDiagnosticsState
    {
        internal readonly bool Shattered;
        internal readonly bool CollisionDisabled;
        internal readonly bool HasVisual;
        internal readonly Transform3D VisualTransform;

        internal PaneDiagnosticsState(
            bool shattered,
            bool collisionDisabled,
            bool hasVisual,
            Transform3D visualTransform)
        {
            Shattered = shattered;
            CollisionDisabled = collisionDisabled;
            HasVisual = hasVisual;
            VisualTransform = visualTransform;
        }
    }

    internal DiagnosticsSnapshot CaptureStateForDiagnostics()
    {
        var panes = new PaneDiagnosticsState[_panes.Count];
        for (var index = 0; index < _panes.Count; index++)
        {
            panes[index] = new PaneDiagnosticsState(
                _panes[index].Shattered,
                IsShapeOwnerDisabled(_panes[index].ShapeOwner),
                _glassMultiMesh is not null,
                _glassMultiMesh?.GetInstanceTransform(index) ?? Transform3D.Identity);
        }
        return new DiagnosticsSnapshot(
            ShatteredCount,
            LastShatterPosition,
            Visible,
            panes);
    }

    internal void RestoreStateForDiagnostics(DiagnosticsSnapshot snapshot)
    {
        if (snapshot.Panes.Length != _panes.Count)
        {
            return;
        }
        Visible = snapshot.Visible;
        ShatteredCount = snapshot.ShatteredCount;
        LastShatterPosition = snapshot.LastShatterPosition;
        for (var index = 0; index < _panes.Count; index++)
        {
            var state = snapshot.Panes[index];
            _panes[index].Shattered = state.Shattered;
            ShapeOwnerSetDisabled(_panes[index].ShapeOwner, state.CollisionDisabled);
            if (state.HasVisual && _glassMultiMesh is not null)
            {
                _glassMultiMesh.SetInstanceTransform(index, state.VisualTransform);
            }
        }
    }

    internal bool MatchesStateForDiagnostics(DiagnosticsSnapshot snapshot)
    {
        if (snapshot.Panes.Length != _panes.Count
            || ShatteredCount != snapshot.ShatteredCount
            || LastShatterPosition != snapshot.LastShatterPosition
            || Visible != snapshot.Visible)
        {
            return false;
        }
        for (var index = 0; index < _panes.Count; index++)
        {
            var state = snapshot.Panes[index];
            if (_panes[index].Shattered != state.Shattered
                || IsShapeOwnerDisabled(_panes[index].ShapeOwner) != state.CollisionDisabled
                || (_glassMultiMesh is not null) != state.HasVisual
                || state.HasVisual && _glassMultiMesh!.GetInstanceTransform(index) != state.VisualTransform)
            {
                return false;
            }
        }
        return true;
    }
}
