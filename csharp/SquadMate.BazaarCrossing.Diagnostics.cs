using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    internal int BazaarRoutePhysicsStepsForDiagnostics { get; private set; }

    /// <summary>
    /// Runs the production objective-navigation motor without enabling the rest of the AI.
    /// This keeps Bazaar traversal diagnostics deterministic while still exercising the
    /// CharacterBody3D collision path used by a live squad mate.
    /// </summary>
    internal void StepBazaarRoutePhysicsForDiagnostics(
        SquadNavigationDirective directive,
        float delta)
    {
        BazaarRoutePhysicsStepsForDiagnostics++;
        var precise = directive.PreciseTrail || directive.Required;
        UpdateTacticalMovement(
            directive.Target,
            hostile: null,
            objectivePriority: true,
            directive.Kind,
            directive.SteppedDirect,
            precise,
            delta);
        MaintainStairNavigation(directive.Target, delta);
        MoveAndSlide();
        BreakableGlassField.TryShatterMovementBlockerFromCollisions(
            this,
            spawnEffects: false);
        TryNavigationStepUp(
            directive.Kind == SquadTraversalKind.Step
                || directive.SteppedDirect
                || precise
                ? _combatPathDirection
                : _combatDesiredDirection,
            directive.Target);
        TrackTacticalMovement(delta);
    }
}
