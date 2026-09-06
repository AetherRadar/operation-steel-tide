using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

internal static partial class CombatModelLibrary
{
    private enum PreviewOperatorBuildStage
    {
        SourceCreated,
        WrapperOwnsSource
    }

    internal static PreviewOperatorOwnershipInspection InspectPreviewOperatorOwnershipForDiagnostics()
    {
        Node3D? sourceStageSource = null;
        var sourceStageFailureCaught = false;
        try
        {
            _ = InstantiatePreviewOperator(
                OperatorVisualId.Garrison,
                (stage, source, _) =>
                {
                    if (stage != PreviewOperatorBuildStage.SourceCreated)
                    {
                        return;
                    }

                    sourceStageSource = source;
                    throw new PreviewOperatorDiagnosticException();
                });
        }
        catch (PreviewOperatorDiagnosticException)
        {
            sourceStageFailureCaught = true;
        }
        catch
        {
            // An unexpected production failure is represented by a false inspection result.
        }

        var sourceFreedBeforeWrapper = sourceStageSource is not null
            && !GodotObject.IsInstanceValid(sourceStageSource);

        Node3D? wrapperStageSource = null;
        Node3D? failedWrapper = null;
        var wrapperStageFailureCaught = false;
        try
        {
            _ = InstantiatePreviewOperator(
                OperatorVisualId.Garrison,
                (stage, source, wrapper) =>
                {
                    if (stage != PreviewOperatorBuildStage.WrapperOwnsSource)
                    {
                        return;
                    }

                    wrapperStageSource = source;
                    failedWrapper = wrapper;
                    throw new PreviewOperatorDiagnosticException();
                });
        }
        catch (PreviewOperatorDiagnosticException)
        {
            wrapperStageFailureCaught = true;
        }
        catch
        {
            // An unexpected production failure is represented by a false inspection result.
        }

        var wrapperFreedAfterOwnership = failedWrapper is not null
            && !GodotObject.IsInstanceValid(failedWrapper);
        var wrappedSourceFreed = wrapperStageSource is not null
            && !GodotObject.IsInstanceValid(wrapperStageSource);

        Node3D? successfulSource = null;
        Node3D? successfulWrapper = null;
        AuthoredPreviewOperatorVisual? successfulVisual = null;
        var successOwnershipTransferred = false;
        try
        {
            successfulVisual = InstantiatePreviewOperator(
                OperatorVisualId.Garrison,
                (stage, source, wrapper) =>
                {
                    if (stage == PreviewOperatorBuildStage.WrapperOwnsSource)
                    {
                        successfulSource = source;
                        successfulWrapper = wrapper;
                    }
                });
            successOwnershipTransferred = successfulWrapper is not null
                && successfulSource is not null
                && ReferenceEquals(successfulVisual.Root, successfulWrapper)
                && GodotObject.IsInstanceValid(successfulWrapper)
                && GodotObject.IsInstanceValid(successfulSource)
                && ReferenceEquals(successfulSource.GetParent(), successfulWrapper);
        }
        catch
        {
            // An unexpected production failure is represented by a false inspection result.
        }
        finally
        {
            if (GodotObject.IsInstanceValid(successfulVisual?.Root))
            {
                successfulVisual!.Root.Free();
            }
        }

        var callerCleanupReleasesTree = successfulWrapper is not null
            && successfulSource is not null
            && !GodotObject.IsInstanceValid(successfulWrapper)
            && !GodotObject.IsInstanceValid(successfulSource);
        return new PreviewOperatorOwnershipInspection(
            sourceStageFailureCaught,
            sourceFreedBeforeWrapper,
            wrapperStageFailureCaught,
            wrapperFreedAfterOwnership,
            wrappedSourceFreed,
            successOwnershipTransferred,
            callerCleanupReleasesTree);
    }

    internal readonly record struct PreviewOperatorOwnershipInspection(
        bool SourceStageFailureCaught,
        bool SourceFreedBeforeWrapper,
        bool WrapperStageFailureCaught,
        bool WrapperFreedAfterOwnership,
        bool WrappedSourceFreed,
        bool SuccessOwnershipTransferred,
        bool CallerCleanupReleasesTree)
    {
        public bool Valid => SourceStageFailureCaught
            && SourceFreedBeforeWrapper
            && WrapperStageFailureCaught
            && WrapperFreedAfterOwnership
            && WrappedSourceFreed
            && SuccessOwnershipTransferred
            && CallerCleanupReleasesTree;
    }

    private sealed class PreviewOperatorDiagnosticException : Exception
    {
    }

    /// <summary>
    /// Builds every operator preview and reports the upright roll applied to
    /// each. Rolls beyond <see cref="PreviewUprightSanityMaximumRadians"/>
    /// mean the asset needs pipeline attention instead of a bigger correction.
    /// </summary>
    internal static PreviewOperatorUprightInspection InspectPreviewOperatorUprightForDiagnostics()
    {
        var rolls = new Dictionary<OperatorVisualId, float>();
        var allBuilt = true;
        foreach (var visualId in new[]
        {
            OperatorVisualId.Garrison,
            OperatorVisualId.Viper,
            OperatorVisualId.Heron,
            OperatorVisualId.Lynx,
            OperatorVisualId.Magpie,
            OperatorVisualId.Jackal,
        })
        {
            AuthoredPreviewOperatorVisual? visual = null;
            try
            {
                visual = InstantiatePreviewOperator(visualId);
                rolls[visualId] = visual.UprightRollRadians;
            }
            catch
            {
                allBuilt = false;
                rolls[visualId] = float.NaN;
            }
            finally
            {
                if (GodotObject.IsInstanceValid(visual?.Root))
                {
                    visual!.Root.Free();
                }
            }
        }
        return new PreviewOperatorUprightInspection(rolls, allBuilt);
    }

    internal readonly record struct PreviewOperatorUprightInspection(
        Dictionary<OperatorVisualId, float> UprightRollRadiansByVisual,
        bool AllBuilt)
    {
        public bool Valid => AllBuilt
            && UprightRollRadiansByVisual.Values.All(
                roll => !float.IsNaN(roll) && Mathf.Abs(roll) <= PreviewUprightSanityMaximumRadians);
    }
}
