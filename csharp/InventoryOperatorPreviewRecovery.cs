using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Isolates authored operator preview failures from UI and world initialization.
/// Failed visuals are remembered for the process lifetime to avoid repeated resource errors.
/// </summary>
internal static class InventoryOperatorPreviewRecovery
{
    private static readonly HashSet<OperatorVisualId> UnavailableVisuals = new();

    public static bool Build(
        Node3D root,
        OperatorVisualId requestedVisual,
        WeaponBuild? weaponBuild = null)
        => TryBuild(
            root,
            requestedVisual,
            visualId => CombatModelLibrary.InstantiatePreviewOperator(visualId, weaponBuild).Root,
            static message => GD.PushError(message)).Built;

    private static OperatorPreviewBuildResult TryBuild(
        Node3D root,
        OperatorVisualId requestedVisual,
        Func<OperatorVisualId, Node3D> instantiate,
        Action<string> reportFailure)
    {
        if (TryAttach(root, requestedVisual, instantiate, reportFailure))
        {
            return new OperatorPreviewBuildResult(true, requestedVisual, false);
        }

        if (requestedVisual != OperatorVisualId.Garrison
            && TryAttach(root, OperatorVisualId.Garrison, instantiate, reportFailure))
        {
            return new OperatorPreviewBuildResult(true, OperatorVisualId.Garrison, true);
        }

        return new OperatorPreviewBuildResult(false, requestedVisual, false);
    }

    private static bool TryAttach(
        Node3D root,
        OperatorVisualId visualId,
        Func<OperatorVisualId, Node3D> instantiate,
        Action<string> reportFailure)
    {
        if (UnavailableVisuals.Contains(visualId))
        {
            return false;
        }

        Node3D? previewRoot = null;
        try
        {
            previewRoot = instantiate(visualId)
                ?? throw new InvalidOperationException("Authored operator factory returned no model root.");
            root.AddChild(previewRoot);
            return true;
        }
        catch (Exception exception)
        {
            if (GodotObject.IsInstanceValid(previewRoot))
            {
                previewRoot!.Free();
            }

            UnavailableVisuals.Add(visualId);
            var recovery = visualId == OperatorVisualId.Garrison
                ? "No authored fallback remains, so the preview will stay empty."
                : "The authored Garrison operator preview will be used when available.";
            reportFailure(
                $"Authored inventory operator preview {visualId} is unavailable. {recovery} "
                + $"{exception.GetType().Name}: {exception.Message}");
            return false;
        }
    }

    internal static OperatorPreviewFailureInspection InspectFailureHandlingForDiagnostics()
    {
        var fallbackPrimaryAttempts = 0;
        var fallbackGarrisonAttempts = 0;
        var fallbackReports = 0;
        Node3D InstantiateWithFallback(OperatorVisualId visualId)
        {
            if (visualId == OperatorVisualId.FemaleFieldOperator)
            {
                fallbackPrimaryAttempts++;
                throw new InvalidOperationException("Simulated requested operator preview failure.");
            }

            fallbackGarrisonAttempts++;
            return new Node3D { Name = "DiagnosticAuthoredGarrisonPreview" };
        }

        UnavailableVisuals.Clear();
        var firstFallbackRoot = new Node3D();
        var firstFallback = TryBuild(
            firstFallbackRoot,
            OperatorVisualId.FemaleFieldOperator,
            InstantiateWithFallback,
            _ => fallbackReports++);
        var secondFallbackRoot = new Node3D();
        var secondFallback = TryBuild(
            secondFallbackRoot,
            OperatorVisualId.FemaleFieldOperator,
            InstantiateWithFallback,
            _ => fallbackReports++);
        var authoredFallbackReady = firstFallback.Built
            && firstFallback.UsedFallback
            && firstFallback.BuiltVisual == OperatorVisualId.Garrison
            && secondFallback.Built
            && secondFallback.UsedFallback
            && firstFallbackRoot.GetChildCount() == 1
            && secondFallbackRoot.GetChildCount() == 1;
        var requestedFailureSuppressed = fallbackPrimaryAttempts == 1
            && fallbackGarrisonAttempts == 2
            && fallbackReports == 1;

        var emptyPrimaryAttempts = 0;
        var emptyGarrisonAttempts = 0;
        var emptyReports = 0;
        Node3D InstantiateNothing(OperatorVisualId visualId)
        {
            if (visualId == OperatorVisualId.FemaleFieldOperator)
            {
                emptyPrimaryAttempts++;
            }
            else
            {
                emptyGarrisonAttempts++;
            }

            throw new InvalidOperationException("Simulated total authored preview failure.");
        }

        UnavailableVisuals.Clear();
        var firstEmptyRoot = new Node3D();
        var firstEmpty = TryBuild(
            firstEmptyRoot,
            OperatorVisualId.FemaleFieldOperator,
            InstantiateNothing,
            _ => emptyReports++);
        var secondEmptyRoot = new Node3D();
        var secondEmpty = TryBuild(
            secondEmptyRoot,
            OperatorVisualId.FemaleFieldOperator,
            InstantiateNothing,
            _ => emptyReports++);
        var emptyFallbackSafe = !firstEmpty.Built
            && !secondEmpty.Built
            && firstEmptyRoot.GetChildCount() == 0
            && secondEmptyRoot.GetChildCount() == 0;
        var totalFailureSuppressed = emptyPrimaryAttempts == 1
            && emptyGarrisonAttempts == 1
            && emptyReports == 2;

        firstFallbackRoot.Free();
        secondFallbackRoot.Free();
        firstEmptyRoot.Free();
        secondEmptyRoot.Free();
        UnavailableVisuals.Clear();

        return new OperatorPreviewFailureInspection(
            fallbackPrimaryAttempts,
            fallbackGarrisonAttempts,
            fallbackReports,
            authoredFallbackReady,
            requestedFailureSuppressed,
            emptyPrimaryAttempts,
            emptyGarrisonAttempts,
            emptyReports,
            emptyFallbackSafe,
            totalFailureSuppressed);
    }

    internal readonly record struct OperatorPreviewFailureInspection(
        int FallbackPrimaryAttempts,
        int FallbackGarrisonAttempts,
        int FallbackFailureReports,
        bool AuthoredFallbackReady,
        bool RequestedFailureSuppressed,
        int EmptyPrimaryAttempts,
        int EmptyGarrisonAttempts,
        int EmptyFailureReports,
        bool EmptyFallbackSafe,
        bool TotalFailureSuppressed)
    {
        public bool Valid => FallbackPrimaryAttempts == 1
            && FallbackGarrisonAttempts == 2
            && FallbackFailureReports == 1
            && AuthoredFallbackReady
            && RequestedFailureSuppressed
            && EmptyPrimaryAttempts == 1
            && EmptyGarrisonAttempts == 1
            && EmptyFailureReports == 2
            && EmptyFallbackSafe
            && TotalFailureSuppressed;
    }

    private readonly record struct OperatorPreviewBuildResult(
        bool Built,
        OperatorVisualId BuiltVisual,
        bool UsedFallback);
}
