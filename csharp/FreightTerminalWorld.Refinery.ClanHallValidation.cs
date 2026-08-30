using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private bool ValidateClanHallCollision(
        out int wallHits,
        out int rampHits,
        out string summary)
    {
        wallHits = 0;
        rampHits = 0;
        summary = "missing_gate";
        if (_clanHallDoubleGate is not { } door
            || !JianghaiClanHallGateContract.TryResolve(
                _jianghaiOldCityScene?.Root,
                out var gate,
                out summary))
        {
            return false;
        }

        var hallShapes = _levelRoot.FindChild(
                "OldTownGameplayCollision",
                recursive: true,
                owned: false)?
            .GetChildren()
            .OfType<CollisionShape3D>()
            .Where(shape => shape.GetMeta(
                    "gameplay_source_node",
                    string.Empty).AsString()
                == JianghaiClanHallGateContract.SourceName)
            .ToArray() ?? Array.Empty<CollisionShape3D>();
        var expectedRoles = new HashSet<string>(StringComparer.Ordinal)
        {
            "facade_left",
            "facade_right",
            "front_lintel",
            "side_left",
            "side_right",
            "back",
            "threshold",
            "floor",
            "entry_ramp"
        };
        var actualRoles = hallShapes.Select(shape => shape.GetMeta(
                "gameplay_proxy_role",
                string.Empty).AsString())
            .ToHashSet(StringComparer.Ordinal);
        if (hallShapes.Length != expectedRoles.Count
            || hallShapes.Any(shape => shape.Shape is not BoxShape3D)
            || !actualRoles.SetEquals(expectedRoles))
        {
            summary = $"shape_contract_{hallShapes.Length}/{expectedRoles.Count}";
            return false;
        }

        var rampShape = hallShapes.Single(shape => shape.GetMeta(
                "gameplay_proxy_role",
                string.Empty).AsString() == "entry_ramp");
        var rampBasis = rampShape.GlobalBasis.Orthonormalized();
        var rampBasisReady = rampBasis.Determinant() > 0.999f
            && rampBasis.X.Cross(rampBasis.Y).Dot(rampBasis.Z) > 0.999f;
        if (!rampBasisReady)
        {
            summary = $"ramp_basis_{rampBasis.Determinant():0.000}";
            return false;
        }

        ProjectClanHallDiagnosticFootprint(
            gate,
            out var minimumTangent,
            out var maximumTangent,
            out var maximumDepth);
        var probeY = gate.Position.Y + Mathf.Min(1.4f, gate.Height * 0.45f);
        var wallProbes = new[]
        {
            (
                gate.Position + gate.Tangent * (gate.Width * -0.5f - 0.8f)
                    + gate.Outward * 0.8f,
                gate.Position + gate.Tangent * (gate.Width * -0.5f - 0.8f)
                    + gate.Inward * 0.8f),
            (
                gate.Position + gate.Tangent * (gate.Width * 0.5f + 0.8f)
                    + gate.Outward * 0.8f,
                gate.Position + gate.Tangent * (gate.Width * 0.5f + 0.8f)
                    + gate.Inward * 0.8f),
            (
                gate.Position + gate.Tangent * (minimumTangent - 0.8f)
                    + gate.Inward * (maximumDepth * 0.45f),
                gate.Position + gate.Tangent * (minimumTangent + 0.8f)
                    + gate.Inward * (maximumDepth * 0.45f)),
            (
                gate.Position + gate.Tangent * (maximumTangent + 0.8f)
                    + gate.Inward * (maximumDepth * 0.55f),
                gate.Position + gate.Tangent * (maximumTangent - 0.8f)
                    + gate.Inward * (maximumDepth * 0.55f)),
            (
                gate.Position + gate.Inward * (maximumDepth - 0.8f),
                gate.Position + gate.Inward * (maximumDepth + 0.8f))
        };
        foreach (var (fromBase, toBase) in wallProbes)
        {
            var from = fromBase with { Y = probeY };
            var to = toBase with { Y = probeY };
            if (!PhysicsRaycast.TryHit(
                    GetWorld3D().DirectSpaceState,
                    from,
                    to,
                    1,
                    out var hit)
                || hit.Collider is not Node collider
                || collider.Name != "OldTownGameplayCollision")
            {
                summary = $"wall_{wallHits}";
                return false;
            }
            wallHits++;
        }

        var exclusions = BuildRefineryLaneExclusions();
        using var exclusionsBacking = exclusions.AsDisposable();
        var authoredPortalClear = !PhysicsRaycast.HasHit(
            GetWorld3D(), door.OutsideProbe, door.InsideProbe, exclusions, 1);
        var closedDoorBlocks = ValidateDoorRaySamples(
            door,
            expectBlocked: true,
            out var closedSamples)
            && closedSamples == 3;
        foreach (var outwardDistance in new[] { 3.5f, 2.0f, 0.5f })
        {
            var position = gate.Position + gate.Outward * outwardDistance;
            if (!PhysicsRaycast.TryHit(
                    GetWorld3D().DirectSpaceState,
                    position + Vector3.Up * 1.6f,
                    position + Vector3.Down * 1.5f,
                    exclusions,
                    1,
                    out var hit)
                || hit.Collider is not Node collider
                || collider.Name != "OldTownGameplayCollision")
            {
                summary = $"ramp_{rampHits}";
                return false;
            }
            rampHits++;
        }

        summary = $"portal={authoredPortalClear}:closed={closedDoorBlocks}:basis={rampBasisReady}";
        return wallHits == 5 && rampHits == 3
            && authoredPortalClear && closedDoorBlocks;
    }

    private static void ProjectClanHallDiagnosticFootprint(
        JianghaiClanHallGateGeometry gate,
        out float minimumTangent,
        out float maximumTangent,
        out float maximumDepth)
    {
        minimumTangent = float.PositiveInfinity;
        maximumTangent = float.NegativeInfinity;
        maximumDepth = float.NegativeInfinity;
        foreach (var x in new[]
        {
            JianghaiClanHallGateContract.WorldMinimumX,
            JianghaiClanHallGateContract.WorldMaximumX
        })
        {
            foreach (var z in new[]
            {
                JianghaiClanHallGateContract.WorldMinimumZ,
                JianghaiClanHallGateContract.WorldMaximumZ
            })
            {
                var delta = new Vector3(x, gate.Position.Y, z) - gate.Position;
                var tangent = delta.Dot(gate.Tangent);
                minimumTangent = Mathf.Min(minimumTangent, tangent);
                maximumTangent = Mathf.Max(maximumTangent, tangent);
                maximumDepth = Mathf.Max(maximumDepth, delta.Dot(gate.Inward));
            }
        }
    }
}
