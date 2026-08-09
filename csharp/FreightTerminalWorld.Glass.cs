using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void CaptureGlassBreak()
    {
        await WaitFrames(6);
        var field = GetTree().GetNodesInGroup(BreakableGlassField.GroupName)
            .OfType<BreakableGlassField>()
            .FirstOrDefault(candidate => candidate.PaneCount >= 2);
        if (field is null || !field.TryGetIntactPaneRay(out var rayFrom, out var rayTo, out _))
        {
            GD.PushError("Glass capture could not find an intact pane.");
            GetTree().Quit(2);
            return;
        }

        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        var center = (rayFrom + rayTo) * 0.5f;
        var outward = center - field.GlobalPosition;
        outward.Y = 0.0f;
        outward = outward.LengthSquared() > 0.001f ? outward.Normalized() : center.DirectionTo(rayFrom);
        rayFrom = center + outward * 1.2f;
        rayTo = center - outward * 1.2f;
        var standPosition = center + outward * 4.2f;
        standPosition.Y = Mathf.Max(0.12f, center.Y - 1.57f);
        _player.GlobalPosition = standPosition;
        _player.FaceWorldPointForDiagnostics(center);
        _player.GrantFireablePrimaryForDiagnostics();
        await WaitFrames(3);

        var shattered = BreakableGlassField.TryShatterAlongRay(
            GetWorld3D(),
            rayFrom,
            rayTo,
            30.0f,
            rayFrom.DirectionTo(rayTo),
            out var hitPosition);
        await WaitFrames(2);
        SaveViewportImage("res://glass_break_validation.png");
        GD.Print($"GLASS_CAPTURE shattered={shattered} hit={hitPosition} path=glass_break_validation.png");
        GetTree().Quit(shattered ? 0 : 2);
    }

    private async void ValidateBreakableGlass()
    {
        await WaitFrames(6);
        var fields = GetTree().GetNodesInGroup(BreakableGlassField.GroupName)
            .OfType<BreakableGlassField>()
            .Where(IsInstanceValid)
            .ToArray();
        var paneCount = fields.Sum(field => field.PaneCount);
        var frameInstances = fields.Sum(field => field.FrameInstanceCount);
        var field = fields.FirstOrDefault(candidate => candidate.PaneCount >= 2);
        var rayFrom = Vector3.Zero;
        var rayTo = Vector3.Zero;
        var paneIndex = -1;
        var rayReady = field is not null
            && field.TryGetIntactPaneRay(out rayFrom, out rayTo, out paneIndex);
        var shattered = false;
        var collisionDisabled = false;
        var repeatIgnored = false;
        if (rayReady && field is not null)
        {
            shattered = BreakableGlassField.TryShatterAlongRay(
                GetWorld3D(),
                rayFrom,
                rayTo,
                30.0f,
                rayFrom.DirectionTo(rayTo),
                out _,
                false);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            collisionDisabled = field.IsPaneShattered(paneIndex)
                && field.IsPaneCollisionDisabled(paneIndex);
            repeatIgnored = !BreakableGlassField.TryShatterAlongRay(
                GetWorld3D(),
                rayFrom,
                rayTo,
                30.0f,
                rayFrom.DirectionTo(rayTo),
                out _,
                false);
        }

        var fieldsBatched = fields.Length > 0
            && fields.Length <= ResidentialTowerSpecs.Length + ResidentialSkyLinks.Sum(link => link.Floors.Length) + 2;
        var panesDense = paneCount >= 1200;
        var framesComplete = frameInstances == paneCount * 5;
        var residentialTracked = ResidentialGlassPaneCount == paneCount;
        var valid = rayReady
            && shattered
            && collisionDisabled
            && repeatIgnored
            && fieldsBatched
            && panesDense
            && framesComplete
            && residentialTracked;
        GD.Print($"GLASS_CHECK valid={valid} fields={fields.Length} panes={paneCount} frames={frameInstances} ray_ready={rayReady} shattered={shattered} collision_disabled={collisionDisabled} repeat_ignored={repeatIgnored} batched={fieldsBatched} dense={panesDense} tracked={residentialTracked}");
        GD.Print($"GLASS_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
