using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateResidentialSquadStairs()
    {
        var cases = 0;
        var completed = 0;
        var stalls = 0;
        var maximumRecoveryCount = 0;
        var minimumProgress = float.PositiveInfinity;
        var failure = string.Empty;
        try
        {
            await WaitFrames(6);
            _missionDirector.ExitDeploymentZone();
            var mate = _squadMates.FirstOrDefault(candidate =>
                IsInstanceValid(candidate) && !candidate.IsHumanProxy && !candidate.IsDowned);
            if (mate is null || _residentialTowers.Count == 0)
            {
                throw new InvalidOperationException("missing residential tower or squad mate");
            }

            foreach (var candidate in _squadMates.Where(candidate =>
                         IsInstanceValid(candidate) && candidate != mate))
            {
                candidate.ProcessMode = ProcessModeEnum.Disabled;
                candidate.GlobalPosition = new Vector3(360.0f + candidate.SquadSlot * 3.0f, 0.3f, 360.0f);
            }
            foreach (var enemy in _enemies.Where(IsInstanceValid))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
                enemy.GlobalPosition = new Vector3(380.0f, 0.3f, 380.0f);
            }

            _player.ProcessMode = ProcessModeEnum.Disabled;
            _player.GlobalPosition = new Vector3(390.0f, 0.3f, 390.0f);
            _player.Velocity = Vector3.Zero;
            SetSquadLeaderTrailForDiagnostics(Array.Empty<Vector3>());

            var tower = _residentialTowers[0];
            var spec = ResidentialTowerSpecs[0];
            var coreZ = -Mathf.Min(spec.Footprint.Y * 0.18f, 3.6f);
            foreach (var laneOffset in new[] { -0.48f, 0.0f, 0.48f })
            {
                foreach (var descending in new[] { false, true })
                {
                    cases++;
                    var route = BuildResidentialSquadStairRoute(tower, coreZ, laneOffset, descending);
                    mate.ProcessMode = ProcessModeEnum.Disabled;
                    mate.GlobalPosition = route[0];
                    mate.Velocity = Vector3.Zero;
                    mate.ResetCombatTacticsForDiagnostics();
                    mate.GrantFireablePrimaryForDiagnostics();
                    mate.SetOrder(SquadOrder.Move, route[0]);
                    await WaitFrames(4);
                    var recoveriesBefore = mate.CombatStuckRecoveries;
                    mate.ProcessMode = ProcessModeEnum.Inherit;

                    var caseCompleted = true;
                    var caseMinimumProgress = float.PositiveInfinity;
                    for (var targetIndex = 1; targetIndex < route.Count; targetIndex++)
                    {
                        var target = route[targetIndex];
                        mate.SetOrder(SquadOrder.Move, target);
                        var segmentStart = mate.GlobalPosition;
                        var segmentLength = Mathf.Max(0.01f, segmentStart.DistanceTo(target));
                        var segmentBestProgress = 0.0f;
                        var reached = false;
                        for (var frame = 0; frame < 240; frame++)
                        {
                            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                            var progress = segmentStart.DistanceTo(mate.GlobalPosition) / segmentLength;
                            segmentBestProgress = Mathf.Max(segmentBestProgress, progress);
                            if (mate.GlobalPosition.DistanceTo(target) <= 0.8f)
                            {
                                reached = true;
                                break;
                            }
                        }
                        caseMinimumProgress = Mathf.Min(caseMinimumProgress, segmentBestProgress);
                        if (!reached)
                        {
                            caseCompleted = false;
                            stalls++;
                            GD.Print(
                                $"RESIDENTIAL_SQUAD_STAIRS_STALL lane={laneOffset:0.00} descending={descending} "
                                + $"target={targetIndex}/{route.Count - 1} progress={segmentBestProgress:0.00} "
                                + $"pos=({mate.GlobalPosition.X:0.00},{mate.GlobalPosition.Y:0.00},{mate.GlobalPosition.Z:0.00}) "
                                + $"target_pos=({target.X:0.00},{target.Y:0.00},{target.Z:0.00})");
                            break;
                        }
                    }

                    completed += caseCompleted ? 1 : 0;
                    minimumProgress = Mathf.Min(minimumProgress, caseMinimumProgress);
                    maximumRecoveryCount = Math.Max(
                        maximumRecoveryCount,
                        mate.CombatStuckRecoveries - recoveriesBefore);
                }
            }
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ":" + exception.Message;
            GD.PushError($"RESIDENTIAL_SQUAD_STAIRS_EXCEPTION {failure}");
        }

        if (float.IsPositiveInfinity(minimumProgress))
        {
            minimumProgress = 0.0f;
        }
        var valid = cases == 6
            && completed == cases
            && stalls == 0
            && string.IsNullOrEmpty(failure);
        GD.Print(
            $"RESIDENTIAL_SQUAD_STAIRS_CHECK valid={valid} cases={cases} completed={completed} "
            + $"stalls={stalls} min_progress={minimumProgress:0.00} max_recoveries={maximumRecoveryCount} failure={failure}");
        GD.Print($"RESIDENTIAL_SQUAD_STAIRS_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private static List<Vector3> BuildResidentialSquadStairRoute(
        Node3D tower,
        float coreZ,
        float laneOffset,
        bool descending)
    {
        var halfRise = ResidentialFloorHeight * 0.5f;
        var stepRise = halfRise / ResidentialStepsPerFlight;
        var stepRun = ResidentialStairRun / ResidentialStepsPerFlight;
        var lowerStart = coreZ - ResidentialStairRun * 0.5f;
        var upperStart = coreZ + ResidentialStairRun * 0.5f;
        var points = new List<Vector3>
        {
            tower.ToGlobal(new Vector3(-1.45f + laneOffset, 0.12f, upperStart + 0.35f))
        };

        for (var step = 2; step < ResidentialStepsPerFlight; step += 3)
        {
            points.Add(tower.ToGlobal(new Vector3(
                -1.45f + laneOffset,
                stepRise * (step + 1) + 0.075f,
                upperStart - stepRun * (step + 0.5f))));
        }
        points.Add(tower.ToGlobal(new Vector3(
            -1.45f + laneOffset,
            halfRise + 0.075f,
            lowerStart - 0.55f)));
        foreach (var x in new[] { -0.65f, 0.25f, 1.15f, 1.45f - laneOffset })
        {
            points.Add(tower.ToGlobal(new Vector3(x, halfRise + 0.075f, lowerStart - 0.55f)));
        }
        for (var step = 2; step < ResidentialStepsPerFlight; step += 3)
        {
            points.Add(tower.ToGlobal(new Vector3(
                1.45f - laneOffset,
                halfRise + stepRise * (step + 1) + 0.075f,
                lowerStart + stepRun * (step + 0.5f))));
        }
        points.Add(tower.ToGlobal(new Vector3(
            1.45f - laneOffset,
            ResidentialFloorHeight + 0.075f,
            upperStart + 0.75f)));

        if (descending)
        {
            points.Reverse();
        }
        return points;
    }
}
