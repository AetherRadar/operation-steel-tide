using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    internal readonly record struct ScriptedObjectiveNavigationDiagnosticState(
        bool Avoiding,
        float Side,
        Vector3 ProgressOrigin,
        float ProgressTimer);

    internal ScriptedObjectiveNavigationDiagnosticState CaptureScriptedObjectiveNavigationForDiagnostics()
        => new(
            _scriptedObjectiveAvoiding,
            _scriptedObjectiveSide,
            _scriptedObjectiveProgressOrigin,
            _scriptedObjectiveProgressTimer);

    internal void RestoreScriptedObjectiveNavigationForDiagnostics(
        ScriptedObjectiveNavigationDiagnosticState state)
    {
        _scriptedObjectiveAvoiding = state.Avoiding;
        _scriptedObjectiveSide = state.Side;
        _scriptedObjectiveProgressOrigin = state.ProgressOrigin;
        _scriptedObjectiveProgressTimer = state.ProgressTimer;
    }
}
