using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateExtractionAiDeployment()
    {
        await WaitFrames(8);
        EnsureAiSquadFill();
        _missionDirector.ExitDeploymentZone();

        var mates = _squadMates
            .Where(mate => IsInstanceValid(mate) && !mate.IsHumanProxy)
            .OrderBy(mate => mate.SquadSlot)
            .Take(2)
            .ToArray();
        var target = _enemies.FirstOrDefault(enemy =>
            IsInstanceValid(enemy)
            && !enemy.IsRivalSquad
            && !enemy.IsWorldBoss
            && !enemy.IsDead);
        if (mates.Length != 2 || target is null)
        {
            GD.Print("EXTRACTION_AI_DEPLOYMENT_CHECK valid=False reason=missing_actors");
            GD.Print("EXTRACTION_AI_DEPLOYMENT_PASS valid=False");
            GetTree().Quit(2);
            return;
        }

        foreach (var enemy in _enemies.Where(IsInstanceValid))
        {
            if (enemy == target)
            {
                continue;
            }
            enemy.ProcessMode = ProcessModeEnum.Disabled;
            enemy.GlobalPosition = new Vector3(240.0f + enemy.NetworkId, 0.2f, 240.0f);
        }

        var arena = new Vector3(8.0f, 0.2f, 18.0f);
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _player.GlobalPosition = arena;
        _player.Velocity = Vector3.Zero;
        ResetSquadLeaderTrail(arena);
        target.ProcessMode = ProcessModeEnum.Inherit;
        target.GlobalPosition = arena + new Vector3(0.0f, 0.0f, 11.0f);
        target.Velocity = Vector3.Zero;
        target.GrantFireablePrimaryForDiagnostics();
        target.SetAlerted(arena);

        foreach (var mate in mates)
        {
            mate.GlobalPosition = arena + new Vector3(
                mate.SquadSlot == 1 ? -2.4f : 2.4f,
                0.0f,
                2.5f);
            mate.Velocity = Vector3.Zero;
            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.SetPhysicsProcess(false);
            mate.ResumeFromExtractionDeployment();
        }

        var initialPositions = mates.Select(mate => mate.GlobalPosition).ToArray();
        var initialRotations = mates.Select(mate => mate.Rotation.Y).ToArray();
        var physicsRestored = mates.All(mate => mate.IsPhysicsProcessing());
        var targetAcquired = false;
        var moved = false;
        var facedContact = false;
        var shotsBefore = mates.Sum(mate => mate.CombatShotsFired);
        for (var frame = 0; frame < 150; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            targetAcquired |= mates.Any(mate => mate.CombatTargetForDiagnostics == target);
            for (var index = 0; index < mates.Length; index++)
            {
                var mate = mates[index];
                moved |= mate.GlobalPosition.DistanceTo(initialPositions[index]) > 0.45f;
                var toTarget = mate.GlobalPosition.DirectionTo(target.GlobalPosition);
                toTarget.Y = 0.0f;
                facedContact |= toTarget.LengthSquared() > 0.01f
                    && Mathf.Abs(Mathf.AngleDifference(
                        mate.Rotation.Y,
                        Mathf.Atan2(-toTarget.X, -toTarget.Z))) < 0.55f;
            }
            if (targetAcquired && moved && facedContact)
            {
                break;
            }
        }

        var shotsFired = mates.Sum(mate => mate.CombatShotsFired) > shotsBefore;
        var valid = physicsRestored && targetAcquired && moved && facedContact && shotsFired;
        var maximumRotationDelta = 0.0f;
        for (var index = 0; index < mates.Length; index++)
        {
            maximumRotationDelta = Mathf.Max(
                maximumRotationDelta,
                Mathf.Abs(Mathf.AngleDifference(mates[index].Rotation.Y, initialRotations[index])));
        }
        GD.Print(
            $"EXTRACTION_AI_DEPLOYMENT_CHECK valid={valid} physics={physicsRestored} "
            + $"target={targetAcquired} moved={moved} faced={facedContact} shots={shotsFired} "
            + $"rotation_delta={maximumRotationDelta:0.00}");
        GD.Print($"EXTRACTION_AI_DEPLOYMENT_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
